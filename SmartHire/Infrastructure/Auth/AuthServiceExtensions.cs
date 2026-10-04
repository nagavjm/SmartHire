using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SmartHire.Data;
using SmartHire.Entities;

namespace SmartHire.Infrastructure.Auth;

/// <summary>
/// Shared AuthBridge OIDC authentication configuration so future downstream applications
/// (e.g. Report Generator) can reuse the exact same pattern.
/// </summary>
public static class AuthServiceExtensions
{
    public const string AppAccessClaimType = "app_access";
    public const string RequiredAppAccessValue = "RESUME_AI";
    public const string RequireAppAccessPolicy = "RequireResumeAiAccess";

    public static IServiceCollection ConfigureAuthServices(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        var authority = configuration["Oidc:Authority"] ?? "https://localhost:7199";
        var clientId = configuration["Oidc:ClientId"] ?? "RESUME_AI";
        // Public-facing origin the browser actually uses (the Angular dev-server proxy on 7399
        // forwards /auth/** to this backend on 7400 without passing the original Host header,
        // so the OIDC handler would otherwise build redirect_uri against 7400). This must match
        // the redirect_uri registered with AuthBridge.
        var publicOrigin = configuration["Oidc:PublicOrigin"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = authority;
                options.ClientId = clientId;
                options.RequireHttpsMetadata = false;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.SaveTokens = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("application_access");

                if (isDevelopment)
                {
                    // AuthBridge uses a self-signed dev certificate locally. The OIDC backchannel
                    // HttpClient (used for metadata discovery and the token-endpoint code exchange)
                    // validates certificates independently of RequireHttpsMetadata, so without this
                    // the backchannel silently fails and the token endpoint never resolves.
                    var devHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    };
                    options.BackchannelHttpHandler = devHandler;

                    // The lazy ConfigurationManager intermittently re-resolves to a null
                    // configuration between the challenge and the /signin-oidc callback (observed
                    // locally against AuthBridge's dev instance), which leaves the token endpoint
                    // unresolved during code redemption. Pre-fetch and pin the discovery document
                    // once at startup so Options.Configuration is always populated.
                    try
                    {
                        using var discoveryClient = new HttpClient(devHandler, disposeHandler: false);
                        var retriever = new HttpDocumentRetriever(discoveryClient) { RequireHttps = false };
                        var fetchedConfig = OpenIdConnectConfigurationRetriever
                            .GetAsync($"{authority.TrimEnd('/')}/.well-known/openid-configuration", retriever, CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                        options.Configuration = fetchedConfig;
                        // Pin a StaticConfigurationManager explicitly so the framework's
                        // PostConfigureOpenIdConnectOptions never overwrites this with a dynamic,
                        // lazily-refreshing ConfigurationManager (observed to intermittently yield
                        // a configuration with a null TokenEndpoint between the challenge and the
                        // /signin-oidc callback against AuthBridge's dev instance).
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(fetchedConfig);
                        Console.WriteLine($"[OIDC-STARTUP] Pre-fetched configuration. TokenEndpoint={fetchedConfig.TokenEndpoint ?? "NULL"}, AuthorizationEndpoint={fetchedConfig.AuthorizationEndpoint ?? "NULL"}");
                    }
                    catch (Exception ex)
                    {
                        // Fall back to the default lazy ConfigurationManager if the eager fetch fails
                        // (e.g. AuthBridge isn't running yet at SmartHire startup).
                        Console.WriteLine($"[OIDC-STARTUP] Pre-fetch FAILED: {ex}");
                    }
                }

                options.Events.OnAuthorizationCodeReceived = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("OidcDiagnostics");
                    logger.LogWarning(
                        "OnAuthorizationCodeReceived: Configuration={Config}, TokenEndpoint={TokenEndpoint}",
                        context.Options.Configuration == null ? "NULL" : "set",
                        context.Options.Configuration?.TokenEndpoint ?? "NULL");
                    return Task.CompletedTask;
                };

                // AuthBridge's ID token only carries baseline identity claims (sub/sid/name/etc.),
                // not application-specific authorization info. SmartHire's own RESUME_AI access
                // grants live locally in Users/UserApplications (populated via SCIM sync or local
                // registration). Enrich the signed-in principal here by looking up (or provisioning)
                // the local User record by the OIDC "sub" claim and adding the app_access claim
                // when that user has an active UserApplications grant for RESUME_AI.
                options.Events.OnTokenValidated = async context =>
                {
                    var principal = context.Principal;
                    if (principal is null)
                    {
                        return;
                    }

                    var subjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? principal.FindFirstValue("sub");
                    if (string.IsNullOrWhiteSpace(subjectId))
                    {
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<SmartHireDbContext>();

                    var user = await db.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
                    if (user is null)
                    {
                        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                        var name = principal.FindFirstValue(ClaimTypes.Name) ?? email;

                        // Fall back to matching an existing SCIM-provisioned user by email so
                        // group-granted access synced via SCIM is picked up on first SSO login.
                        user = !string.IsNullOrWhiteSpace(email)
                            ? await db.Users.FirstOrDefaultAsync(u => u.SubjectId == null && u.Email == email)
                            : null;

                        if (user is null)
                        {
                            user = new User
                            {
                                SubjectId = subjectId,
                                Email = email,
                                DisplayName = string.IsNullOrWhiteSpace(name) ? email : name,
                            };
                            db.Users.Add(user);
                        }
                        else
                        {
                            user.SubjectId = subjectId;
                        }

                        await db.SaveChangesAsync();
                    }

                    user.LastLoginAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    var hasAccess = await db.UserApplications
                        .Include(ua => ua.Application)
                        .AnyAsync(ua => ua.UserId == user.Id && ua.Application!.Code == RequiredAppAccessValue);

                    if (hasAccess)
                    {
                        var identity = (ClaimsIdentity)principal.Identity!;
                        identity.AddClaim(new Claim(AppAccessClaimType, RequiredAppAccessValue));
                    }
                };

                if (!string.IsNullOrWhiteSpace(publicOrigin))
                {
                    var origin = publicOrigin.TrimEnd('/');

                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        context.ProtocolMessage.RedirectUri = origin + options.CallbackPath;
                        return Task.CompletedTask;
                    };

                    options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        context.ProtocolMessage.PostLogoutRedirectUri = origin + options.SignedOutCallbackPath;
                        return Task.CompletedTask;
                    };
                }
            });

        services.AddAuthorization(options =>
        {
            // Claim-based authorization: user must present app_access = RESUME_AI.
            options.AddPolicy(RequireAppAccessPolicy, policy =>
                policy.RequireClaim(AppAccessClaimType, RequiredAppAccessValue));
        });

        return services;
    }
}
