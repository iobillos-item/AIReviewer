namespace AIReviewer.Domain.Entities;

public class UnifiedReviewResult
{
    public string OverallSummary { get; set; } = string.Empty;
    public List<AgentReviewResult> AgentResults { get; set; } = [];
    public List<ReviewViolation> AllViolations => AgentResults.SelectMany(a => a.Violations).ToList();
}
