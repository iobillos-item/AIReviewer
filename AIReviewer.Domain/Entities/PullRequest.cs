namespace AIReviewer.Domain.Entities;

public class PullRequest
{
    public int Id { get; set; }
    public string Repository { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Diff { get; set; } = string.Empty;
}
