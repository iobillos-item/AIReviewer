namespace AIReviewer.Domain.Entities;

public class ReviewViolation
{
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
