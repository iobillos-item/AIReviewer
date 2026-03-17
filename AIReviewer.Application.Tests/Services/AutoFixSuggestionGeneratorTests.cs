using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class AutoFixSuggestionGeneratorTests
{
    private readonly Mock<ILLMService> _llmMock = new();
    private readonly Mock<IPromptBuilder> _promptMock = new();
    private readonly Mock<ILogger<AutoFixSuggestionGenerator>> _loggerMock = new();

    private AutoFixSuggestionGenerator CreateSut() =>
        new(_llmMock.Object, _promptMock.Object, _loggerMock.Object);

    private static AgentViolation CreateViolation() => new()
    {
        AgentName = "Security",
        File = "Auth.cs",
        Line = 10,
        Issue = "Hardcoded secret",
        SuggestedFix = "Use config",
        Severity = "Critical"
    };

    private static DiffChunk CreateChunk() => new()
    {
        FileName = "Auth.cs",
        Content = "+ var secret = \"abc123\";",
        StartLine = 8,
        EndLine = 15
    };

    [Fact]
    public async Task GenerateFixAsync_ParsesCodeBlockFromResponse()
    {
        _promptMock.Setup(p => p.BuildAutoFixPrompt(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync("Move the secret to configuration.\n```csharp\nvar secret = config[\"Secret\"];\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal("Auth.cs", result.OriginalFile);
        Assert.Equal(10, result.Line);
        Assert.Contains("config", result.SuggestedCode);
        Assert.Contains("configuration", result.Explanation);
    }

    [Fact]
    public async Task GenerateFixAsync_NoCodeBlock_ReturnsEmptyCode()
    {
        _promptMock.Setup(p => p.BuildAutoFixPrompt(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync("Just use environment variables instead.");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Empty(result.SuggestedCode);
        Assert.Contains("environment variables", result.Explanation);
    }

    [Fact]
    public async Task GenerateFixAsync_LLMFailure_FallsBackToAgentSuggestion()
    {
        _promptMock.Setup(p => p.BuildAutoFixPrompt(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ThrowsAsync(new HttpRequestException("Rate limited"));

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal("Use config", result.Explanation);
        Assert.Empty(result.SuggestedCode);
    }

    [Fact]
    public async Task GenerateFixAsync_PreservesViolationMetadata()
    {
        _promptMock.Setup(p => p.BuildAutoFixPrompt(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync("Fix it.\n```\nfixed code\n```");

        var violation = CreateViolation();
        var result = await CreateSut().GenerateFixAsync(violation, CreateChunk());

        Assert.Equal(violation.File, result.OriginalFile);
        Assert.Equal(violation.Line, result.Line);
        Assert.Equal(violation.Issue, result.Issue);
    }
}
