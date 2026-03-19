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

    private void SetupPromptAndLlm(string llmResponse)
    {
        _promptMock.Setup(p => p.BuildAutoFixPrompt(It.IsAny<AgentViolation>(), It.IsAny<DiffChunk>()))
            .Returns("prompt");
        _llmMock.Setup(l => l.GetCompletionAsync("prompt"))
            .ReturnsAsync(llmResponse);
    }

    [Fact]
    public async Task GenerateFixAsync_ParsesCodeBlockFromResponse()
    {
        SetupPromptAndLlm("Move the secret to configuration.\n```csharp\nvar secret = config[\"Secret\"];\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal("Auth.cs", result.OriginalFile);
        Assert.Equal(10, result.Line);
        Assert.Contains("config", result.CodeSnippet);
        Assert.Contains("configuration", result.Explanation);
    }

    [Fact]
    public async Task GenerateFixAsync_NoCodeBlock_ReturnsEmptyCode()
    {
        SetupPromptAndLlm("Just use environment variables instead.");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Empty(result.CodeSnippet);
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
        Assert.Empty(result.CodeSnippet);
        Assert.Equal(FixType.SmallSnippet, result.FixType);
    }

    [Fact]
    public async Task GenerateFixAsync_PreservesViolationMetadata()
    {
        SetupPromptAndLlm("Fix it.\n```\nfixed code\n```");

        var violation = CreateViolation();
        var result = await CreateSut().GenerateFixAsync(violation, CreateChunk());

        Assert.Equal(violation.File, result.OriginalFile);
        Assert.Equal(violation.Line, result.Line);
        Assert.Equal(violation.Issue, result.Issue);
    }

    // --- FixType classification tests ---

    [Theory]
    [InlineData(1, FixType.SmallSnippet)]
    [InlineData(3, FixType.SmallSnippet)]
    [InlineData(5, FixType.SmallSnippet)]
    [InlineData(6, FixType.MultiLineSnippet)]
    [InlineData(15, FixType.MultiLineSnippet)]
    [InlineData(20, FixType.MultiLineSnippet)]
    [InlineData(21, FixType.FullFileRefactor)]
    [InlineData(50, FixType.FullFileRefactor)]
    public void ClassifyFixType_ReturnsCorrectType(int lineCount, FixType expected)
    {
        Assert.Equal(expected, AutoFixSuggestionGenerator.ClassifyFixType(lineCount));
    }

    [Fact]
    public void CountLines_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, AutoFixSuggestionGenerator.CountLines(""));
        Assert.Equal(0, AutoFixSuggestionGenerator.CountLines("   "));
    }

    [Fact]
    public void CountLines_MultipleLines_ReturnsCorrectCount()
    {
        var code = "line1\nline2\nline3";
        Assert.Equal(3, AutoFixSuggestionGenerator.CountLines(code));
    }

    [Fact]
    public void TruncateSnippet_UnderLimit_ReturnsOriginal()
    {
        var code = "line1\nline2\nline3";
        Assert.Equal(code, AutoFixSuggestionGenerator.TruncateSnippet(code, 5));
    }

    [Fact]
    public void TruncateSnippet_OverLimit_TruncatesWithComment()
    {
        var lines = string.Join('\n', Enumerable.Range(1, 40).Select(i => $"line{i}"));
        var result = AutoFixSuggestionGenerator.TruncateSnippet(lines, 30);

        Assert.Equal(31, result.Split('\n').Length); // 30 lines + truncation comment
        Assert.EndsWith("// ... truncated for brevity", result);
    }

    [Fact]
    public async Task GenerateFixAsync_SmallSnippet_ClassifiedCorrectly()
    {
        SetupPromptAndLlm("Fix the issue.\n```\nvar x = 1;\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal(FixType.SmallSnippet, result.FixType);
    }

    [Fact]
    public async Task GenerateFixAsync_MultiLineSnippet_ClassifiedCorrectly()
    {
        var codeLines = string.Join('\n', Enumerable.Range(1, 10).Select(i => $"var x{i} = {i};"));
        SetupPromptAndLlm($"Refactor needed.\n```\n{codeLines}\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal(FixType.MultiLineSnippet, result.FixType);
    }

    [Fact]
    public async Task GenerateFixAsync_LargeSnippetWithoutJustification_TruncatesAfterRetries()
    {
        var codeLines = string.Join('\n', Enumerable.Range(1, 35).Select(i => $"var x{i} = {i};"));
        SetupPromptAndLlm($"Big fix.\n```\n{codeLines}\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        // After max retries, should be truncated to MultiLineSnippet
        Assert.Equal(FixType.MultiLineSnippet, result.FixType);
        Assert.StartsWith("[Truncated]", result.Explanation);
    }

    [Fact]
    public async Task GenerateFixAsync_LargeSnippetWithJustification_AllowsFullRefactor()
    {
        var codeLines = string.Join('\n', Enumerable.Range(1, 25).Select(i => $"var x{i} = {i};"));
        SetupPromptAndLlm($"Major refactor.\nJustification: Architecture violation across layers\n```\n{codeLines}\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal(FixType.FullFileRefactor, result.FixType);
        Assert.Equal("Architecture violation across layers", result.FullRefactorJustification);
    }

    [Fact]
    public async Task GenerateFixAsync_SetsFixTypeOnResult()
    {
        SetupPromptAndLlm("Quick fix.\n```\nvar x = 1;\nvar y = 2;\nvar z = 3;\n```");

        var result = await CreateSut().GenerateFixAsync(CreateViolation(), CreateChunk());

        Assert.Equal(FixType.SmallSnippet, result.FixType);
        Assert.NotEmpty(result.CodeSnippet);
    }
}
