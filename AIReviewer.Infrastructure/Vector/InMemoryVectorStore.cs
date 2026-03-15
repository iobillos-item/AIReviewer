using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Infrastructure.Vector;

public class InMemoryVectorStore : IVectorStore
{
    private readonly List<SopEmbedding> _store = new();
    private readonly ILogger<InMemoryVectorStore> _logger;
    private int _nextId = 1;

    public InMemoryVectorStore(ILogger<InMemoryVectorStore> logger)
    {
        _logger = logger;
    }

    public Task StoreEmbeddingAsync(SopEmbedding embedding)
    {
        embedding.Id = _nextId++;
        _store.Add(embedding);
        return Task.CompletedTask;
    }

    public Task StoreBatchAsync(IEnumerable<SopEmbedding> embeddings)
    {
        foreach (var embedding in embeddings)
        {
            embedding.Id = _nextId++;
            _store.Add(embedding);
        }

        _logger.LogInformation("Stored {Count} embeddings in memory", _store.Count);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SopEmbedding>> SearchSimilarAsync(float[] queryEmbedding, int topK = 5)
    {
        var results = _store
            .Select(e => new { Embedding = e, Score = CosineSimilarity(queryEmbedding, e.Embedding) })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Embedding);

        return Task.FromResult(results);
    }

    public Task ClearAllAsync()
    {
        _store.Clear();
        _nextId = 1;
        _logger.LogInformation("Cleared all in-memory embeddings");
        return Task.CompletedTask;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude == 0 ? 0 : dot / magnitude;
    }
}
