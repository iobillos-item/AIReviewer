namespace AIReviewer.Domain.Entities;

public class ReviewResult
{
    public string Summary { get; set; } = string.Empty;
    public List<ReviewViolation> Violations { get; set; } = new();
}
