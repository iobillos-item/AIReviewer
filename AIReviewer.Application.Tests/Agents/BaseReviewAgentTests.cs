using AIReviewer.Application.Agents;
using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Agents;

public class BaseReviewAgentTests
{
    private readonly Mock<ILLMService> _llmMock = new();
    private readonly Mock<IPromptBuilder> _promptMock = new();
    private readonly IResponseParser _parser = new ResponseParser();

    private DiffChunk CreateChunk() => new() { FileName = "Test.cs", Content = "+ code" };

    [Fact]
    public async Task ReviewAsync_ParsesViolationsFromLLMResponse()
    {
        _llmMock.Setup(l => l.GetCompletionAsync(It.IsAny<string>()))
            .ReturnsAsync("""
                Summary
                Found a security issue.

                Violations
                File: Auth.cs
                Line: 15
                Issue: Hardcoded password
                Suggested Fix: Use configuration
                Severity: Critical
                """);
        _promptMock.Setup(p => p.BuildAgentPrompt(It.IsAny<string>(), It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .Returns("prompt");

        var agent = new SecurityReviewAgent(_llmMock.Object, _promptMock.Object, _parser,
            Mock.Of<ILogger<SecurityReviewAgent>>());

        var result = await agent.ReviewAsync(CreateChunk(), "sop rules");

        Assert.Equal("Security", result.AgentName);
        Assert.False(result.HasError);
        Assert.Single(result.Violations);
        Assert.Equal("Critical", result.Violations[0].Severity);
    }

    [Fact]
    public async Task ReviewAsync_LLMFailure_ReturnsErrorResult()
    {
        _llmMock.Setup(l => l.GetCompletionAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Rate limited"));
        _promptMock.Setup(p => p.BuildAgentPrompt(It.IsAny<string>(), It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .Returns("prompt");

        var agent = new ArchitectureReviewAgent(_llmMock.Object, _promptMock.Object, _parser,
            Mock.Of<ILogger<ArchitectureReviewAgent>>());

        var result = await agent.ReviewAsync(CreateChunk(), "sop");

        Assert.True(result.HasError);
        Assert.Contains("Rate limited", result.ErrorMessage);
        Assert.Equal("Architecture", result.AgentName);
    }

    [Fact]
    public async Task ReviewAsync_NoViolations_ReturnsEmptyList()
    {
        _llmMock.Setup(l => l.GetCompletionAsync(It.IsAny<string>()))
            .ReturnsAsync("""
                Summary
                Code looks clean.

                Violations
                """);
        _promptMock.Setup(p => p.BuildAgentPrompt(It.IsAny<string>(), It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .Returns("prompt");

        var agent = new PerformanceReviewAgent(_llmMock.Object, _promptMock.Object, _parser,
            Mock.Of<ILogger<PerformanceReviewAgent>>());

        var result = await agent.ReviewAsync(CreateChunk(), "sop");

        Assert.False(result.HasError);
        Assert.Empty(result.Violations);
        Assert.Equal("Code looks clean.", result.Summary);
    }

    [Fact]
    public async Task ReviewAsync_RecordsDuration()
    {
        _llmMock.Setup(l => l.GetCompletionAsync(It.IsAny<string>()))
            .ReturnsAsync("Summary\nAll good.\n\nViolations\n");
        _promptMock.Setup(p => p.BuildAgentPrompt(It.IsAny<string>(), It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .Returns("prompt");

        var agent = new DependencyReviewAgent(_llmMock.Object, _promptMock.Object, _parser,
            Mock.Of<ILogger<DependencyReviewAgent>>());

        var result = await agent.ReviewAsync(CreateChunk(), "sop");

        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task ReviewAsync_EachAgentReportsCorrectName()
    {
        _llmMock.Setup(l => l.GetCompletionAsync(It.IsAny<string>()))
            .ReturnsAsync("Summary\nOk.\n\nViolations\n");
        _promptMock.Setup(p => p.BuildAgentPrompt(It.IsAny<string>(), It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .Returns("prompt");

        var agents = new ICodeReviewAgent[]
        {
            new ArchitectureReviewAgent(_llmMock.Object, _promptMock.Object, _parser, Mock.Of<ILogger<ArchitectureReviewAgent>>()),
            new SecurityReviewAgent(_llmMock.Object, _promptMock.Object, _parser, Mock.Of<ILogger<SecurityReviewAgent>>()),
            new PerformanceReviewAgent(_llmMock.Object, _promptMock.Object, _parser, Mock.Of<ILogger<PerformanceReviewAgent>>()),
            new TestCoverageReviewAgent(_llmMock.Object, _promptMock.Object, _parser, Mock.Of<ILogger<TestCoverageReviewAgent>>()),
            new DependencyReviewAgent(_llmMock.Object, _promptMock.Object, _parser, Mock.Of<ILogger<DependencyReviewAgent>>())
        };

        var names = new[] { "Architecture", "Security", "Performance", "TestCoverage", "Dependency" };

        for (var i = 0; i < agents.Length; i++)
        {
            var result = await agents[i].ReviewAsync(CreateChunk(), "sop");
            Assert.Equal(names[i], result.AgentName);
        }
    }

    [Fact]
    public async Task ReviewAsync_IsStateless_SameAgentDifferentChunks()
    {
        var callCount = 0;
        _llmMock.Setup(l => l.GetCompletionAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? "Summary\nFirst call.\n\nViolations\nFile: A.cs\nLine: 1\nIssue: X\nSuggested Fix: Y\nSeverity: Minor\n"
                    : "Summary\nSecond call.\n\nViolations\n";
            });
        _promptMock.Setup(p => p.BuildAgentPrompt(It.IsAny<string>(), It.IsAny<DiffChunk>(), It.IsAny<string>()))
            .Returns("prompt");

        var agent = new SecurityReviewAgent(_llmMock.Object, _promptMock.Object, _parser,
            Mock.Of<ILogger<SecurityReviewAgent>>());

        var result1 = await agent.ReviewAsync(new DiffChunk { FileName = "A.cs", Content = "a" }, "sop");
        var result2 = await agent.ReviewAsync(new DiffChunk { FileName = "B.cs", Content = "b" }, "sop");

        // Agent doesn't carry state between calls
        Assert.Single(result1.Violations);
        Assert.Empty(result2.Violations);
        Assert.Equal("First call.", result1.Summary);
        Assert.Equal("Second call.", result2.Summary);
    }
}
