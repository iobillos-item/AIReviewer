namespace AIReviewer.Domain.Entities;

public class UnifiedReviewResult
{
    public string OverallSummary { get; set; } = string.Empty;
    public List<AgentViolation> Violations { get; set; } = new();
    public List<AgentReviewResult> AgentResults { get; set; } = new();
}
