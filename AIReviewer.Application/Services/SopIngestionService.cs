using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class SopIngestionService : ISopIngestionService
{
    private readonly ISopProvider _sopProvider;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<SopIngestionService> _logger;

    private const int ChunkSize = 500; // characters per SOP chunk

    public SopIngestionService(
        ISopProvider sopProvider,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<SopIngestionService> logger)
    {
        _sopProvider = sopProvider;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task IngestAsync()
    {
        _logger.LogInformation("Starting SOP ingestion");

        await _vectorStore.ClearAllAsync();

        var sopContent = await _sopProvider.GetSopContentAsync();
        if (string.IsNullOrWhiteSpace(sopContent))
        {
            _logger.LogWarning("No SOP content found to ingest");
            return;
        }

        var chunks = SplitIntoChunks(sopContent);
        var embeddings = new List<SopEmbedding>();

        foreach (var (content, sourceFile) in chunks)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(content);
            embeddings.Add(new SopEmbedding
            {
                Content = content,
                SourceFile = sourceFile,
                Embedding = vector
            });
        }

        await _vectorStore.StoreBatchAsync(embeddings);
        _logger.LogInformation("Ingested {Count} SOP chunks", embeddings.Count);
    }

    private static List<(string Content, string SourceFile)> SplitIntoChunks(string sopContent)
    {
        var results = new List<(string, string)>();
        var sections = sopContent.Split("---", StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            var trimmed = section.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // First line after --- is typically the filename
            var lines = trimmed.Split('\n', 2);
            var sourceFile = lines[0].Trim();
            var content = lines.Length > 1 ? lines[1].Trim() : sourceFile;

            if (content.Length <= ChunkSize)
            {
                results.Add((content, sourceFile));
            }
            else
            {
                // Split by paragraphs, then by size
                var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                var current = "";

                foreach (var para in paragraphs)
                {
                    if ((current + "\n\n" + para).Length > ChunkSize && current.Length > 0)
                    {
                        results.Add((current.Trim(), sourceFile));
                        current = para;
                    }
                    else
                    {
                        current = string.IsNullOrEmpty(current) ? para : current + "\n\n" + para;
                    }
                }

                if (!string.IsNullOrWhiteSpace(current))
                    results.Add((current.Trim(), sourceFile));
            }
        }

        return results;
    }
}
