using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;
using PgVector = Pgvector.Vector;

namespace AIReviewer.Infrastructure.Vector;

public class VectorStoreService : IVectorStore
{
    private readonly string _connectionString;
    private readonly ILogger<VectorStoreService> _logger;

    public VectorStoreService(IConfiguration configuration, ILogger<VectorStoreService> logger)
    {
        _connectionString = configuration.GetConnectionString("VectorDb")
            ?? throw new InvalidOperationException("VectorDb connection string not configured");
        _logger = logger;
    }

    public async Task StoreEmbeddingAsync(SopEmbedding embedding)
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO sop_embeddings (content, source_file, embedding) VALUES (@content, @source, @embedding)", conn);

        cmd.Parameters.AddWithValue("content", embedding.Content);
        cmd.Parameters.AddWithValue("source", embedding.SourceFile);
        cmd.Parameters.AddWithValue("embedding", new PgVector(embedding.Embedding));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task StoreBatchAsync(IEnumerable<SopEmbedding> embeddings)
    {
        await using var conn = await CreateConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        foreach (var embedding in embeddings)
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO sop_embeddings (content, source_file, embedding) VALUES (@content, @source, @embedding)", conn);

            cmd.Parameters.AddWithValue("content", embedding.Content);
            cmd.Parameters.AddWithValue("source", embedding.SourceFile);
            cmd.Parameters.AddWithValue("embedding", new PgVector(embedding.Embedding));

            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<IEnumerable<SopEmbedding>> SearchSimilarAsync(float[] queryEmbedding, int topK = 5)
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, content, source_file FROM sop_embeddings ORDER BY embedding <-> @query LIMIT @topk", conn);

        cmd.Parameters.AddWithValue("query", new PgVector(queryEmbedding));
        cmd.Parameters.AddWithValue("topk", topK);

        var results = new List<SopEmbedding>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new SopEmbedding
            {
                Id = reader.GetInt32(0),
                Content = reader.GetString(1),
                SourceFile = reader.GetString(2)
            });
        }

        return results;
    }

    public async Task ClearAllAsync()
    {
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM sop_embeddings", conn);
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Cleared all SOP embeddings");
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();
        var conn = await dataSource.OpenConnectionAsync();
        return conn;
    }
}
