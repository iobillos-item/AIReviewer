using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class ReviewCoordinatorTests
{
    private readonly Mock<IGitHubService> _gitHubMock = new();
    private readonly Mock<IDiffChunker> _chunkerMock = new();
    private readonly Mock<ISopContextRetriever> _sopRetrieverMock = new();
    private readonly Mock<IReviewAggregator> _aggregatorMock = new();
    private readonly Mock<IReviewCommentFormatter> _formatterMock = new();
    private readonly Mock<ILogger<ReviewCoordinator>> _loggerMock = new();

    private void SetupDefaults(params DiffChunk[] chunks)
    {
        _gitHubMock.Setup(g => g.GetPullRequestDiffAsync("repo", 1)).ReturnsAsync("diff");
        _chunkerMock.Setup(c => c.ChunkAsync("diff")).ReturnsAsync(chunks);
        _sopRetrieverMock.Setup(s => s.GetRelevantContextAsync(It.IsAny<string>())).ReturnsAsync("sop");
        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Done" });
        _formatterMock.Setup(f => f.Format(It.IsAny<UnifiedReviewResult>())).Returns("comment");
        _gitHubMock.Setup(g => g.PostPullRequestCommentAsync("repo", 1, It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private ReviewCoordinator CreateSut(params ICodeReviewAgent[] agents) =>
        new(_gitHubMock.Object, _chunkerMock.Object, _sopRetrieverMock.Object,
            agents, _aggregatorMock.Object, _formatterMock.Object, _loggerMock.Object);

    private static Mock<ICodeReviewAgent> CreateAgent(string name)
    {
        var mock = new Mock<ICodeReviewAgent>();
        mock.Setup(a => a.AgentName).Returns(name);
        mock.Setup(a => a.ReviewAsync(It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .ReturnsAsync(new AgentReviewResult { AgentName = name });
        return mock;
    }

    [Fact]
    public async Task ReviewAsync_ExecutesAllAgentsOnAllChunks()
    {
        var chunk = new DiffChunk { FileName = "Test.cs", Content = "+ code" };
        SetupDefaults(chunk);

        var agent1 = CreateAgent("Agent1");
        var agent2 = CreateAgent("Agent2");

        var result = await CreateSut(agent1.Object, agent2.Object).ReviewAsync("repo", 1);

        agent1.Verify(a => a.ReviewAsync(chunk, "sop"), Times.Once);
        agent2.Verify(a => a.ReviewAsync(chunk, "sop"), Times.Once);
        Assert.Equal("Done", result.OverallSummary);
    }

    [Fact]
    public async Task ReviewAsync_MultipleChunks_RunsAgentsOnEach()
    {
        var chunk1 = new DiffChunk { FileName = "A.cs", Content = "a" };
        var chunk2 = new DiffChunk { FileName = "B.cs", Content = "b" };
        SetupDefaults(chunk1, chunk2);

        var agent = CreateAgent("Agent");
        await CreateSut(agent.Object).ReviewAsync("repo", 1);

        agent.Verify(a => a.ReviewAsync(It.IsAny<DiffChunk>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReviewAsync_PostsFormattedComment()
    {
        SetupDefaults(new DiffChunk { FileName = "X.cs", Content = "x" });
        _formatterMock.Setup(f => f.Format(It.IsAny<UnifiedReviewResult>())).Returns("formatted comment");

        await CreateSut(CreateAgent("A").Object).ReviewAsync("repo", 1);

        _gitHubMock.Verify(g => g.PostPullRequestCommentAsync("repo", 1, "formatted comment"), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_RetrievesSopContextPerChunk()
    {
        var chunk1 = new DiffChunk { FileName = "A.cs", Content = "content-a" };
        var chunk2 = new DiffChunk { FileName = "B.cs", Content = "content-b" };
        SetupDefaults(chunk1, chunk2);

        await CreateSut(CreateAgent("A").Object).ReviewAsync("repo", 1);

        _sopRetrieverMock.Verify(s => s.GetRelevantContextAsync("content-a"), Times.Once);
        _sopRetrieverMock.Verify(s => s.GetRelevantContextAsync("content-b"), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_NoAgents_StillAggregatesAndPosts()
    {
        SetupDefaults(new DiffChunk { FileName = "X.cs", Content = "x" });

        var result = await CreateSut().ReviewAsync("repo", 1);

        _aggregatorMock.Verify(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()), Times.Once);
        _gitHubMock.Verify(g => g.PostPullRequestCommentAsync("repo", 1, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_AgentErrorResult_StillPassedToAggregator()
    {
        SetupDefaults(new DiffChunk { FileName = "X.cs", Content = "x" });

        var agent = new Mock<ICodeReviewAgent>();
        agent.Setup(a => a.AgentName).Returns("Failing");
        agent.Setup(a => a.ReviewAsync(It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .ReturnsAsync(new AgentReviewResult { AgentName = "Failing", HasError = true, ErrorMessage = "boom" });

        IEnumerable<AgentReviewResult>? capturedResults = null;
        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Callback<IEnumerable<AgentReviewResult>>(r => capturedResults = r.ToList())
            .Returns(new UnifiedReviewResult { OverallSummary = "Partial" });

        await CreateSut(agent.Object).ReviewAsync("repo", 1);

        Assert.NotNull(capturedResults);
        Assert.Single(capturedResults);
        Assert.True(capturedResults.First().HasError);
    }
}
