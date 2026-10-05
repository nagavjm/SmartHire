using System.Reflection;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartHire.Data;
using SmartHire.Features;
using SmartHire.Infrastructure.Auth;
using SmartHire.Infrastructure.BackgroundServices;
using SmartHire.Infrastructure.BlobStorage;
using SmartHire.Infrastructure.OpenAi;
using SmartHire.Infrastructure.Options;
using SmartHire.Infrastructure.ScimSync;
using SmartHire.Infrastructure.Search;
using SmartHire.Infrastructure.TextExtraction;
using SmartHire.Middleware;
using SmartHire.Repositories;
using SmartHire.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Serilog ----------
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// ---------- MVC / Swagger ----------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- Database ----------
builder.Services.AddDbContext<SmartHireDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Options ----------
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
builder.Services.Configure<AzureAiSearchOptions>(builder.Configuration.GetSection(AzureAiSearchOptions.SectionName));
builder.Services.Configure<AzureOpenAiOptions>(builder.Configuration.GetSection(AzureOpenAiOptions.SectionName));

// ---------- Authentication (AuthBridge OIDC SSO) ----------
builder.Services.ConfigureAuthServices(builder.Configuration, builder.Environment.IsDevelopment());

// ---------- Internal SCIM sync endpoint auth (ScimProvisioning.Api -> SmartHire) ----------
builder.Services
    .AddAuthentication()
    .AddScheme<ScimSyncApiKeyAuthenticationOptions, ScimSyncApiKeyAuthenticationHandler>(
        ScimSyncApiKeyAuthenticationOptions.SchemeName,
        options => options.ApiKey = builder.Configuration["ScimSync:ApiKey"] ?? string.Empty);

// ---------- Application services ----------
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICandidateQueryService, CandidateQueryService>();
builder.Services.AddScoped<IResumeQueryService, ResumeQueryService>();
builder.Services.AddScoped<IScreeningService, ScreeningService>();
builder.Services.AddScoped<IResumeSyncService, ResumeSyncService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();
builder.Services.AddSingleton<IVectorSearchService, AzureAiSearchService>();
builder.Services.AddSingleton<IOpenAiService, AzureOpenAiService>();
builder.Services.AddHostedService<ResumeIndexingBackgroundService>();

// ---------- Cross-cutting ----------
builder.Services.AddAutoMapper(cfg => { }, Assembly.GetExecutingAssembly());
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(FeaturesAssemblyMarker).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(FeaturesAssemblyMarker).Assembly);
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevClient", policy =>
        policy.WithOrigins("https://localhost:7399")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseSmartHireExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AngularDevClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Serve the built Angular SPA from wwwroot in non-development environments.
app.MapFallbackToFile("index.html");

app.Run();
