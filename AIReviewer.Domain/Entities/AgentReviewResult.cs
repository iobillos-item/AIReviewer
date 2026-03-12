namespace AIReviewer.Domain.Entities;

public class AgentReviewResult
{
    public string AgentName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<ReviewViolation> Violations { get; set; } = [];
}
