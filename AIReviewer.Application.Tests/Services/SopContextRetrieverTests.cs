using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class SopContextRetrieverTests
{
    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<IVectorStore> _vectorMock = new();

    [Fact]
    public async Task GetRelevantContextAsync_ReturnsJoinedContent()
    {
        _embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f });
        _vectorMock.Setup(v => v.SearchSimilarAsync(It.IsAny<float[]>(), 5))
            .ReturnsAsync(new List<SopEmbedding>
            {
                new() { Content = "Rule 1: Use async" },
                new() { Content = "Rule 2: No hardcoded secrets" }
            });

        var sut = new SopContextRetriever(_vectorMock.Object, _embeddingMock.Object);
        var result = await sut.GetRelevantContextAsync("some diff");

        Assert.Contains("Rule 1", result);
        Assert.Contains("Rule 2", result);
    }

    [Fact]
    public async Task GetRelevantContextAsync_NoResults_ReturnsEmpty()
    {
        _embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f });
        _vectorMock.Setup(v => v.SearchSimilarAsync(It.IsAny<float[]>(), 5))
            .ReturnsAsync(new List<SopEmbedding>());

        var sut = new SopContextRetriever(_vectorMock.Object, _embeddingMock.Object);
        var result = await sut.GetRelevantContextAsync("diff");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetRelevantContextAsync_PassesEmbeddingToVectorStore()
    {
        var embedding = new float[] { 0.5f, 0.6f };
        _embeddingMock.Setup(e => e.GenerateEmbeddingAsync("my diff"))
            .ReturnsAsync(embedding);
        _vectorMock.Setup(v => v.SearchSimilarAsync(embedding, 5))
            .ReturnsAsync(new List<SopEmbedding>());

        var sut = new SopContextRetriever(_vectorMock.Object, _embeddingMock.Object);
        await sut.GetRelevantContextAsync("my diff");

        _embeddingMock.Verify(e => e.GenerateEmbeddingAsync("my diff"), Times.Once);
        _vectorMock.Verify(v => v.SearchSimilarAsync(embedding, 5), Times.Once);
    }
}
