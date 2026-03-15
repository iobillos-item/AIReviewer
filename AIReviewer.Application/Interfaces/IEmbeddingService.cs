namespace AIReviewer.Application.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}
