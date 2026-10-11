namespace SmartHire.Entities;

/// <summary>
/// Structured candidate data extracted from a resume document.
/// </summary>
public class CandidateProfile
{
    public int Id { get; set; }

    public int ResumeDocumentId { get; set; }
    public ResumeDocument? ResumeDocument { get; set; }

    public string CandidateName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public double? TotalExperienceYears { get; set; }
    public string? Skills { get; set; } // comma-separated or JSON array
    public string? ResumeSummary { get; set; }
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Status { get; set; }

    public ICollection<ScreeningResult> ScreeningResults { get; set; } = new List<ScreeningResult>();
}
