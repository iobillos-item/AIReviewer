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
    private readonly Mock<IGitHubReviewService> _reviewServiceMock = new();
    private readonly Mock<IDiffChunker> _chunkerMock = new();
    private readonly Mock<IDiffRouterAgent> _routerMock = new();
    private readonly Mock<ISopContextRetriever> _sopRetrieverMock = new();
    private readonly Mock<IMetaReviewAgent> _metaReviewMock = new();
    private readonly Mock<IAutoFixSuggestionGenerator> _fixGenMock = new();
    private readonly Mock<IReviewAggregator> _aggregatorMock = new();
    private readonly Mock<IReviewCommentFormatter> _formatterMock = new();
    private readonly Mock<ILogger<ReviewCoordinator>> _loggerMock = new();

    private void SetupDefaults(params DiffChunk[] chunks)
    {
        _gitHubMock.Setup(g => g.GetPullRequestDiffAsync("repo", 1)).ReturnsAsync("diff");
        _chunkerMock.Setup(c => c.ChunkAsync("diff")).ReturnsAsync(chunks);

        // Router assigns all agents to all chunks by default
        _routerMock.Setup(r => r.RouteAsync(It.IsAny<IEnumerable<DiffChunk>>()))
            .ReturnsAsync((IEnumerable<DiffChunk> c) =>
                c.Select(ch => new RoutedChunk { Chunk = ch, AssignedAgents = new List<string> { "TestAgent" } }));

        _sopRetrieverMock.Setup(s => s.GetRelevantContextAsync(It.IsAny<string>())).ReturnsAsync("sop");

        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Done", Violations = new() });

        _metaReviewMock.Setup(m => m.ValidateAsync(It.IsAny<IEnumerable<AgentViolation>>()))
            .ReturnsAsync((IEnumerable<AgentViolation> v) => v);

        _fixGenMock.Setup(f => f.GenerateFixAsync(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()))
            .ReturnsAsync(new AutoFixResult());

        _formatterMock.Setup(f => f.Format(It.IsAny<UnifiedReviewResult>())).Returns("comment");

        _gitHubMock.Setup(g => g.PostPullRequestCommentAsync("repo", 1, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _reviewServiceMock.Setup(r => r.GetPullRequestHeadShaAsync("repo", 1)).ReturnsAsync("abc123");
        _reviewServiceMock.Setup(r => r.PostInlineCommentAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<AgentViolation>(), It.IsAny<AutoFixResult?>()))
            .Returns(Task.CompletedTask);
    }

    private ReviewCoordinator CreateSut(params ICodeReviewAgent[] agents) =>
        new(_gitHubMock.Object, _reviewServiceMock.Object, _chunkerMock.Object, _routerMock.Object,
            _sopRetrieverMock.Object, agents, _metaReviewMock.Object, _fixGenMock.Object,
            _aggregatorMock.Object, _formatterMock.Object, _loggerMock.Object);

    private static Mock<ICodeReviewAgent> CreateAgent(string name)
    {
        var mock = new Mock<ICodeReviewAgent>();
        mock.Setup(a => a.AgentName).Returns(name);
        mock.Setup(a => a.ReviewAsync(It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .ReturnsAsync(new AgentReviewResult { AgentName = name });
        return mock;
    }

    [Fact]
    public async Task ReviewAsync_RoutesChunksBeforeExecution()
    {
        var chunk = new DiffChunk { FileName = "Test.cs", Content = "+ code" };
        SetupDefaults(chunk);

        var agent = CreateAgent("TestAgent");
        await CreateSut(agent.Object).ReviewAsync("repo", 1);

        _routerMock.Verify(r => r.RouteAsync(It.IsAny<IEnumerable<DiffChunk>>()), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_OnlyExecutesRoutedAgents()
    {
        var chunk = new DiffChunk { FileName = "Test.cs", Content = "+ code" };
        SetupDefaults(chunk);

        // Router only assigns "Security"
        _routerMock.Setup(r => r.RouteAsync(It.IsAny<IEnumerable<DiffChunk>>()))
            .ReturnsAsync(new[] { new RoutedChunk { Chunk = chunk, AssignedAgents = new List<string> { "Security" } } });

        var secAgent = CreateAgent("Security");
        var archAgent = CreateAgent("Architecture");

        await CreateSut(secAgent.Object, archAgent.Object).ReviewAsync("repo", 1);

        secAgent.Verify(a => a.ReviewAsync(chunk, "sop"), Times.Once);
        archAgent.Verify(a => a.ReviewAsync(It.IsAny<DiffChunk>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReviewAsync_CallsMetaReviewAfterAggregation()
    {
        var chunk = new DiffChunk { FileName = "Test.cs", Content = "+ code" };
        SetupDefaults(chunk);

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "Security", File = "Test.cs", Line = 1, Issue = "Issue", Severity = "Major" }
        };

        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Done", Violations = violations });

        await CreateSut(CreateAgent("TestAgent").Object).ReviewAsync("repo", 1);

        _metaReviewMock.Verify(m => m.ValidateAsync(violations), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_GeneratesFixesForCriticalAndMajor()
    {
        var chunk = new DiffChunk { FileName = "Test.cs", Content = "+ code" };
        SetupDefaults(chunk);

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "Sec", File = "Test.cs", Line = 1, Issue = "Critical issue", Severity = "Critical" },
            new() { AgentName = "Arch", File = "Test.cs", Line = 5, Issue = "Minor issue", Severity = "Minor" }
        };

        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Done", Violations = violations });

        await CreateSut(CreateAgent("TestAgent").Object).ReviewAsync("repo", 1);

        // Only Critical/Major get fixes
        _fixGenMock.Verify(f => f.GenerateFixAsync(
            It.Is<AgentViolation>(v => v.Severity == "Critical"), It.IsAny<DiffChunk>()), Times.Once);
        _fixGenMock.Verify(f => f.GenerateFixAsync(
            It.Is<AgentViolation>(v => v.Severity == "Minor"), It.IsAny<DiffChunk>()), Times.Never);
    }

    [Fact]
    public async Task ReviewAsync_PostsInlineCommentsForEachViolation()
    {
        var chunk = new DiffChunk { FileName = "Test.cs", Content = "+ code" };
        SetupDefaults(chunk);

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "Sec", File = "Test.cs", Line = 1, Issue = "A", Severity = "Major" },
            new() { AgentName = "Arch", File = "Test.cs", Line = 5, Issue = "B", Severity = "Minor" }
        };

        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Done", Violations = violations });

        await CreateSut(CreateAgent("TestAgent").Object).ReviewAsync("repo", 1);

        _reviewServiceMock.Verify(r => r.PostInlineCommentAsync(
            "repo", 1, "abc123", It.IsAny<AgentViolation>(), It.IsAny<AutoFixResult?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReviewAsync_PostsSummaryComment()
    {
        SetupDefaults(new DiffChunk { FileName = "X.cs", Content = "x" });
        _formatterMock.Setup(f => f.Format(It.IsAny<UnifiedReviewResult>())).Returns("formatted comment");

        await CreateSut(CreateAgent("TestAgent").Object).ReviewAsync("repo", 1);

        _gitHubMock.Verify(g => g.PostPullRequestCommentAsync("repo", 1, "formatted comment"), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_FetchesHeadShaForInlineComments()
    {
        SetupDefaults(new DiffChunk { FileName = "X.cs", Content = "x" });

        await CreateSut(CreateAgent("TestAgent").Object).ReviewAsync("repo", 1);

        _reviewServiceMock.Verify(r => r.GetPullRequestHeadShaAsync("repo", 1), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_NoViolations_SkipsFixGenAndInlineComments()
    {
        SetupDefaults(new DiffChunk { FileName = "X.cs", Content = "x" });

        _aggregatorMock.Setup(a => a.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Clean", Violations = new() });

        await CreateSut(CreateAgent("TestAgent").Object).ReviewAsync("repo", 1);

        _fixGenMock.Verify(f => f.GenerateFixAsync(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()), Times.Never);
        _reviewServiceMock.Verify(r => r.PostInlineCommentAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<AgentViolation>(), It.IsAny<AutoFixResult?>()), Times.Never);
    }
}
