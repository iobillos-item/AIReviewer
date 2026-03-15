namespace AIReviewer.Domain.Entities;

public class DiffChunk
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
