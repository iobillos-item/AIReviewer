namespace AIReviewer.Domain.Entities;

public class SopEmbedding
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
