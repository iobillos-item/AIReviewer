using AIReviewer.Application.Interfaces;

namespace AIReviewer.Application.Services;

public class SopContextRetriever : ISopContextRetriever
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;

    public SopContextRetriever(IVectorStore vectorStore, IEmbeddingService embeddingService)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    public async Task<string> GetRelevantContextAsync(string diffChunk)
    {
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(diffChunk);
        var results = await _vectorStore.SearchSimilarAsync(queryEmbedding, topK: 5);

        var relevantRules = results.Select(r => r.Content);
        return string.Join("\n\n", relevantRules);
    }
}
