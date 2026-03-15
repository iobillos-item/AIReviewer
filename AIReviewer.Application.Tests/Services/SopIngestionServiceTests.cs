using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class SopIngestionServiceTests
{
    private readonly Mock<ISopProvider> _sopMock = new();
    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<IVectorStore> _vectorMock = new();
    private readonly Mock<ILogger<SopIngestionService>> _loggerMock = new();
    private readonly SopIngestionService _sut;

    public SopIngestionServiceTests()
    {
        _sut = new SopIngestionService(
            _sopMock.Object, _embeddingMock.Object,
            _vectorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task IngestAsync_ClearsExistingEmbeddings()
    {
        _sopMock.Setup(s => s.GetSopContentAsync()).ReturnsAsync("--- file.md ---\nSome rule.");
        _embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f });

        await _sut.IngestAsync();

        _vectorMock.Verify(v => v.ClearAllAsync(), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_EmptyContent_DoesNotStore()
    {
        _sopMock.Setup(s => s.GetSopContentAsync()).ReturnsAsync(string.Empty);

        await _sut.IngestAsync();

        _vectorMock.Verify(v => v.ClearAllAsync(), Times.Once);
        _vectorMock.Verify(v => v.StoreBatchAsync(It.IsAny<IEnumerable<SopEmbedding>>()), Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WithContent_GeneratesEmbeddingsAndStores()
    {
        _sopMock.Setup(s => s.GetSopContentAsync())
            .ReturnsAsync("--- security-rules.md ---\nNo hardcoded secrets.\n--- coding-standards.md ---\nUse async.");
        _embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        await _sut.IngestAsync();

        _embeddingMock.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>()), Times.AtLeast(2));
        _vectorMock.Verify(v => v.StoreBatchAsync(It.Is<IEnumerable<SopEmbedding>>(
            batch => batch.Count() >= 2)), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_WhitespaceOnlyContent_DoesNotStore()
    {
        _sopMock.Setup(s => s.GetSopContentAsync()).ReturnsAsync("   \n\n   ");

        await _sut.IngestAsync();

        _vectorMock.Verify(v => v.StoreBatchAsync(It.IsAny<IEnumerable<SopEmbedding>>()), Times.Never);
    }
}
