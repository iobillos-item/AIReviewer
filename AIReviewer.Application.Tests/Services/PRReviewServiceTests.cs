using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class PRReviewServiceTests
{
    private readonly Mock<IGitHubService> _gitHubServiceMock;
    private readonly Mock<ISopProvider> _sopProviderMock;
    private readonly Mock<IReviewAggregator> _aggregatorMock;
    private readonly Mock<ILogger<PRReviewService>> _loggerMock;

    private const string Repo = "owner/repo";
    private const int PrNumber = 42;

    public PRReviewServiceTests()
    {
        _gitHubServiceMock = new Mock<IGitHubService>();
        _sopProviderMock = new Mock<ISopProvider>();
        _aggregatorMock = new Mock<IReviewAggregator>();
        _loggerMock = new Mock<ILogger<PRReviewService>>();

        _gitHubServiceMock
            .Setup(x => x.GetPullRequestDiffAsync(Repo, PrNumber))
            .ReturnsAsync("some diff");
        _sopProviderMock
            .Setup(x => x.GetSopContentAsync())
            .ReturnsAsync("sop rules");
        _gitHubServiceMock
            .Setup(x => x.PostPullRequestCommentAsync(Repo, PrNumber, It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private PRReviewService CreateService(params ICodeReviewAgent[] agents)
    {
        return new PRReviewService(
            _gitHubServiceMock.Object,
            _sopProviderMock.Object,
            agents,
            _aggregatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_ExecutesAllAgentsInParallel()
    {
        // Arrange
        var agent1 = CreateMockAgent("Architecture", new AgentReviewResult { AgentName = "Architecture", Summary = "Clean" });
        var agent2 = CreateMockAgent("Security", new AgentReviewResult { AgentName = "Security", Summary = "Secure" });

        _aggregatorMock
            .Setup(x => x.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "All good" });

        var sut = CreateService(agent1.Object, agent2.Object);

        // Act
        await sut.ReviewPullRequestAsync(Repo, PrNumber);

        // Assert
        agent1.Verify(a => a.ReviewAsync("some diff", "sop rules"), Times.Once);
        agent2.Verify(a => a.ReviewAsync("some diff", "sop rules"), Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_PostsCommentAfterAggregation()
    {
        // Arrange
        var agent = CreateMockAgent("Architecture", new AgentReviewResult { AgentName = "Architecture" });

        _aggregatorMock
            .Setup(x => x.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Summary" });

        var sut = CreateService(agent.Object);

        // Act
        await sut.ReviewPullRequestAsync(Repo, PrNumber);

        // Assert
        _gitHubServiceMock.Verify(
            x => x.PostPullRequestCommentAsync(Repo, PrNumber, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_AgentFailure_ContinuesWithOtherAgents()
    {
        // Arrange
        var failingAgent = new Mock<ICodeReviewAgent>();
        failingAgent.Setup(a => a.AgentName).Returns("Failing");
        failingAgent.Setup(a => a.ReviewAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("LLM timeout"));

        var healthyAgent = CreateMockAgent("Security", new AgentReviewResult { AgentName = "Security", Summary = "OK" });

        _aggregatorMock
            .Setup(x => x.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "Partial" });

        var sut = CreateService(failingAgent.Object, healthyAgent.Object);

        // Act
        var result = await sut.ReviewPullRequestAsync(Repo, PrNumber);

        // Assert — should not throw, healthy agent still executed
        healthyAgent.Verify(a => a.ReviewAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _gitHubServiceMock.Verify(
            x => x.PostPullRequestCommentAsync(Repo, PrNumber, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_NoAgents_PostsEmptyReview()
    {
        // Arrange
        _aggregatorMock
            .Setup(x => x.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult { OverallSummary = "No agents" });

        var sut = CreateService(); // no agents

        // Act
        var result = await sut.ReviewPullRequestAsync(Repo, PrNumber);

        // Assert
        Assert.Equal("No agents", result.Summary);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_ReturnsAggregatedViolations()
    {
        // Arrange
        var violation = new ReviewViolation
        {
            AgentName = "Security",
            File = "Auth.cs",
            Line = 10,
            Issue = "Hardcoded secret",
            Severity = "Critical"
        };

        var agentResult = new AgentReviewResult
        {
            AgentName = "Security",
            Summary = "Found issue",
            Violations = [violation]
        };

        var agent = CreateMockAgent("Security", agentResult);

        _aggregatorMock
            .Setup(x => x.Aggregate(It.IsAny<IEnumerable<AgentReviewResult>>()))
            .Returns(new UnifiedReviewResult
            {
                OverallSummary = "Issues found",
                AgentResults = [agentResult]
            });

        var sut = CreateService(agent.Object);

        // Act
        var result = await sut.ReviewPullRequestAsync(Repo, PrNumber);

        // Assert
        Assert.Single(result.Violations);
        Assert.Equal("Security", result.Violations[0].AgentName);
        Assert.Equal("Hardcoded secret", result.Violations[0].Issue);
    }

    private static Mock<ICodeReviewAgent> CreateMockAgent(string name, AgentReviewResult result)
    {
        var mock = new Mock<ICodeReviewAgent>();
        mock.Setup(a => a.AgentName).Returns(name);
        mock.Setup(a => a.ReviewAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(result);
        return mock;
    }
}
