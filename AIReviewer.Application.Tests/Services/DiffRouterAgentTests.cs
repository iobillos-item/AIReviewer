using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class DiffRouterAgentTests
{
    private readonly Mock<ILLMService> _llmMock = new();
    private readonly Mock<IPromptBuilder> _promptMock = new();
    private readonly Mock<ILogger<DiffRouterAgent>> _loggerMock = new();

    private DiffRouterAgent CreateSut(params string[] agentNames)
    {
        var agents = agentNames.Select(name =>
        {
            var mock = new Mock<ICodeReviewAgent>();
            mock.Setup(a => a.AgentName).Returns(name);
            return mock.Object;
        });

        return new DiffRouterAgent(_llmMock.Object, _promptMock.Object, agents, _loggerMock.Object);
    }

    [Fact]
    public async Task RouteAsync_AssignsAgentsBasedOnLLMResponse()
    {
        _promptMock.Setup(p => p.BuildRouterPrompt(It.IsAny<DiffChunk>(), It.IsAny<IEnumerable<string>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync("Security\nArchitecture");

        var chunk = new DiffChunk { FileName = "AuthController.cs", Content = "+ code" };
        var sut = CreateSut("Architecture", "Security", "Performance");

        var result = (await sut.RouteAsync(new[] { chunk })).ToList();

        Assert.Single(result);
        Assert.Contains("Security", result[0].AssignedAgents);
        Assert.Contains("Architecture", result[0].AssignedAgents);
        Assert.DoesNotContain("Performance", result[0].AssignedAgents);
    }

    [Fact]
    public async Task RouteAsync_FallsBackToAllAgents_WhenLLMReturnsNoMatch()
    {
        _promptMock.Setup(p => p.BuildRouterPrompt(It.IsAny<DiffChunk>(), It.IsAny<IEnumerable<string>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync("I don't know which agents to use.");

        var chunk = new DiffChunk { FileName = "Unknown.txt", Content = "text" };
        var sut = CreateSut("Architecture", "Security");

        var result = (await sut.RouteAsync(new[] { chunk })).ToList();

        Assert.Equal(2, result[0].AssignedAgents.Count);
    }

    [Fact]
    public async Task RouteAsync_FallsBackToAllAgents_WhenLLMThrows()
    {
        _promptMock.Setup(p => p.BuildRouterPrompt(It.IsAny<DiffChunk>(), It.IsAny<IEnumerable<string>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ThrowsAsync(new HttpRequestException("Rate limited"));

        var chunk = new DiffChunk { FileName = "Test.cs", Content = "code" };
        var sut = CreateSut("Architecture", "Security");

        var result = (await sut.RouteAsync(new[] { chunk })).ToList();

        Assert.Equal(2, result[0].AssignedAgents.Count);
    }

    [Fact]
    public async Task RouteAsync_RoutesMultipleChunksIndependently()
    {
        _promptMock.Setup(p => p.BuildRouterPrompt(It.IsAny<DiffChunk>(), It.IsAny<IEnumerable<string>>()))
            .Returns("prompt");

        var callCount = 0;
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? "Security" : "Performance";
            });

        var chunks = new[]
        {
            new DiffChunk { FileName = "Auth.cs", Content = "auth" },
            new DiffChunk { FileName = "Query.cs", Content = "query" }
        };

        var sut = CreateSut("Security", "Performance");
        var result = (await sut.RouteAsync(chunks)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains("Security", result[0].AssignedAgents);
        Assert.Contains("Performance", result[1].AssignedAgents);
    }
}
