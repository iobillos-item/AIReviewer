using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IVectorStore
{
    Task StoreEmbeddingAsync(SopEmbedding embedding);
    Task StoreBatchAsync(IEnumerable<SopEmbedding> embeddings);
    Task<IEnumerable<SopEmbedding>> SearchSimilarAsync(float[] queryEmbedding, int topK = 5);
    Task ClearAllAsync();
}
