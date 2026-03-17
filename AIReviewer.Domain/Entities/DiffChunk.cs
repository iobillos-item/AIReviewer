namespace AIReviewer.Domain.Entities;

public class DiffChunk
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}

/// <summary>
/// A diff chunk paired with the agents that should review it (determined by the DiffRouterAgent).
/// </summary>
public class RoutedChunk
{
    public DiffChunk Chunk { get; set; } = new();
    public List<string> AssignedAgents { get; set; } = new();
}
