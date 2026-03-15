namespace AIReviewer.Domain.Entities;

public class AgentReviewResult
{
    public string AgentName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<AgentViolation> Violations { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AgentViolation
{
    public string AgentName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
