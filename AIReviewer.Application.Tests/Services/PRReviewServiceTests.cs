using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class PRReviewServiceTests
{
    private readonly Mock<IGitHubService> _gitHubMock = new();
    private readonly Mock<ILLMService> _llmMock = new();
    private readonly Mock<ISopProvider> _sopMock = new();
    private readonly Mock<IPromptBuilder> _promptMock = new();
    private readonly IResponseParser _parser = new ResponseParser();
    private readonly Mock<ILogger<PRReviewService>> _loggerMock = new();
    private readonly PRReviewService _sut;

    private const string Repo = "owner/repo";
    private const int PrNumber = 42;

    public PRReviewServiceTests()
    {
        _sut = new PRReviewService(
            _gitHubMock.Object, _llmMock.Object, _sopMock.Object,
            _promptMock.Object, _parser, _loggerMock.Object);
    }

    private void SetupMocks(string aiResponse)
    {
        _gitHubMock.Setup(x => x.GetPullRequestDiffAsync(Repo, PrNumber)).ReturnsAsync("diff");
        _sopMock.Setup(x => x.GetSopContentAsync()).ReturnsAsync("sop");
        _promptMock.Setup(x => x.BuildReviewPrompt(It.IsAny<string>(), It.IsAny<string>())).Returns("prompt");
        _llmMock.Setup(x => x.GetCompletionAsync(It.IsAny<string>())).ReturnsAsync(aiResponse);
        _gitHubMock.Setup(x => x.PostPullRequestCommentAsync(Repo, PrNumber, It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_WithViolations_ReturnsCorrectResult()
    {
        SetupMocks("""
            Summary
            Found one issue.

            Violations
            File: Program.cs
            Line: 10
            Issue: Use const
            Suggested Fix: Change var to const
            Severity: Minor
            """);

        var result = await _sut.ReviewPullRequestAsync(Repo, PrNumber);

        Assert.Equal("Found one issue.", result.Summary);
        Assert.Single(result.Violations);
        Assert.Equal("Program.cs", result.Violations[0].File);
        Assert.Equal(10, result.Violations[0].Line);
        Assert.Equal("Minor", result.Violations[0].Severity);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_NoViolations_ReturnsEmpty()
    {
        SetupMocks("""
            Summary
            Code looks clean.

            Violations
            """);

        var result = await _sut.ReviewPullRequestAsync(Repo, PrNumber);

        Assert.Equal("Code looks clean.", result.Summary);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_PostsComment()
    {
        SetupMocks("Summary\nOk.\n\nViolations\n");

        await _sut.ReviewPullRequestAsync(Repo, PrNumber);

        _gitHubMock.Verify(x => x.PostPullRequestCommentAsync(Repo, PrNumber, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_EmptyResponse_ReturnsEmptyResult()
    {
        SetupMocks(string.Empty);

        var result = await _sut.ReviewPullRequestAsync(Repo, PrNumber);

        Assert.Equal(string.Empty, result.Summary);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_MultipleViolations_ParsesAll()
    {
        SetupMocks("""
            Summary
            Issues found.

            Violations
            File: A.cs
            Line: 1
            Issue: Issue A
            Suggested Fix: Fix A
            Severity: Critical
            File: B.cs
            Line: 2
            Issue: Issue B
            Suggested Fix: Fix B
            Severity: Major
            """);

        var result = await _sut.ReviewPullRequestAsync(Repo, PrNumber);

        Assert.Equal(2, result.Violations.Count);
        Assert.Equal("Critical", result.Violations[0].Severity);
        Assert.Equal("Major", result.Violations[1].Severity);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_CallsPromptBuilderWithCorrectArgs()
    {
        _gitHubMock.Setup(x => x.GetPullRequestDiffAsync(Repo, PrNumber)).ReturnsAsync("the-diff");
        _sopMock.Setup(x => x.GetSopContentAsync()).ReturnsAsync("the-sop");
        _promptMock.Setup(x => x.BuildReviewPrompt("the-sop", "the-diff")).Returns("built-prompt");
        _llmMock.Setup(x => x.GetCompletionAsync("built-prompt")).ReturnsAsync("Summary\nOk.\n\nViolations\n");
        _gitHubMock.Setup(x => x.PostPullRequestCommentAsync(Repo, PrNumber, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _sut.ReviewPullRequestAsync(Repo, PrNumber);

        _promptMock.Verify(x => x.BuildReviewPrompt("the-sop", "the-diff"), Times.Once);
        _llmMock.Verify(x => x.GetCompletionAsync("built-prompt"), Times.Once);
    }
}
