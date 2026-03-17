using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class MetaReviewAgentTests
{
    private readonly Mock<ILLMService> _llmMock = new();
    private readonly Mock<IPromptBuilder> _promptMock = new();
    private readonly Mock<ILogger<MetaReviewAgent>> _loggerMock = new();

    private MetaReviewAgent CreateSut() => new(_llmMock.Object, _promptMock.Object, _loggerMock.Object);

    [Fact]
    public async Task ValidateAsync_KeepAll_ReturnsAllViolations()
    {
        _promptMock.Setup(p => p.BuildMetaReviewPrompt(It.IsAny<IEnumerable<AgentViolation>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt")).ReturnsAsync("keep_all");

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "Security", Issue = "A", File = "X.cs", Line = 1, Severity = "Critical" },
            new() { AgentName = "Arch", Issue = "B", File = "Y.cs", Line = 2, Severity = "Minor" }
        };

        var result = (await CreateSut().ValidateAsync(violations)).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ValidateAsync_FiltersByIndices()
    {
        _promptMock.Setup(p => p.BuildMetaReviewPrompt(It.IsAny<IEnumerable<AgentViolation>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt")).ReturnsAsync("[0, 2]");

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "A", Issue = "Keep", File = "A.cs", Line = 1, Severity = "Critical" },
            new() { AgentName = "B", Issue = "Remove", File = "B.cs", Line = 2, Severity = "Minor" },
            new() { AgentName = "C", Issue = "Keep2", File = "C.cs", Line = 3, Severity = "Major" }
        };

        var result = (await CreateSut().ValidateAsync(violations)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Keep", result[0].Issue);
        Assert.Equal("Keep2", result[1].Issue);
    }

    [Fact]
    public async Task ValidateAsync_EmptyInput_ReturnsEmpty()
    {
        var result = (await CreateSut().ValidateAsync(new List<AgentViolation>())).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public async Task ValidateAsync_LLMFailure_ReturnsOriginal()
    {
        _promptMock.Setup(p => p.BuildMetaReviewPrompt(It.IsAny<IEnumerable<AgentViolation>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "A", Issue = "Issue", File = "X.cs", Line = 1, Severity = "Major" }
        };

        var result = (await CreateSut().ValidateAsync(violations)).ToList();

        Assert.Single(result);
        Assert.Equal("Issue", result[0].Issue);
    }

    [Fact]
    public async Task ValidateAsync_MalformedResponse_ReturnsOriginal()
    {
        _promptMock.Setup(p => p.BuildMetaReviewPrompt(It.IsAny<IEnumerable<AgentViolation>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync("This is not valid JSON at all");

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "A", Issue = "Issue", File = "X.cs", Line = 1, Severity = "Major" }
        };

        var result = (await CreateSut().ValidateAsync(violations)).ToList();

        Assert.Single(result);
    }

    [Fact]
    public async Task ValidateAsync_IndicesOutOfRange_IgnoresInvalid()
    {
        _promptMock.Setup(p => p.BuildMetaReviewPrompt(It.IsAny<IEnumerable<AgentViolation>>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt")).ReturnsAsync("[0, 99, -1]");

        var violations = new List<AgentViolation>
        {
            new() { AgentName = "A", Issue = "Valid", File = "X.cs", Line = 1, Severity = "Major" }
        };

        var result = (await CreateSut().ValidateAsync(violations)).ToList();

        Assert.Single(result);
        Assert.Equal("Valid", result[0].Issue);
    }
}
