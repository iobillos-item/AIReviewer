using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class AutoFixSuggestionGenerator : IAutoFixSuggestionGenerator
{
    private const int SmallSnippetMaxLines = 5;
    private const int MultiLineMaxLines = 20;
    private const int SnippetSizeThreshold = 30;
    private const int MaxRegenerationAttempts = 2;

    private readonly ILLMService _llmService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILogger<AutoFixSuggestionGenerator> _logger;

    public AutoFixSuggestionGenerator(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        ILogger<AutoFixSuggestionGenerator> logger)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<AutoFixResult> GenerateFixAsync(AgentViolation violation, DiffChunk chunk)
    {
        try
        {
            var prompt = _promptBuilder.BuildAutoFixPrompt(violation, chunk);
            var result = await GenerateAndValidateAsync(prompt, violation);

            _logger.LogInformation(
                "Generated {FixType} fix for {File}:{Line} ({Agent})",
                result.FixType, violation.File, violation.Line, violation.AgentName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoFix failed for {File}:{Line}", violation.File, violation.Line);

            return new AutoFixResult
            {
                OriginalFile = violation.File,
                Line = violation.Line,
                Issue = violation.Issue,
                FixType = FixType.SmallSnippet,
                CodeSnippet = string.Empty,
                Explanation = violation.SuggestedFix
            };
        }
    }

    private async Task<AutoFixResult> GenerateAndValidateAsync(string prompt, AgentViolation violation)
    {
        for (var attempt = 0; attempt <= MaxRegenerationAttempts; attempt++)
        {
            var response = await _llmService.GetCompletionAsync(prompt);
            var result = ParseFixResponse(response, violation);

            var lineCount = CountLines(result.CodeSnippet);
            result.FixType = ClassifyFixType(lineCount);

            // If snippet exceeds threshold without FullFileRefactor justification, regenerate
            if (lineCount > SnippetSizeThreshold && string.IsNullOrWhiteSpace(result.FullRefactorJustification))
            {
                _logger.LogWarning(
                    "Fix for {File}:{Line} has {Lines} lines without FullFileRefactor justification (attempt {Attempt})",
                    violation.File, violation.Line, lineCount, attempt + 1);

                if (attempt < MaxRegenerationAttempts)
                    continue;

                // Final attempt: truncate and downgrade
                result.CodeSnippet = TruncateSnippet(result.CodeSnippet, SnippetSizeThreshold);
                result.FixType = FixType.MultiLineSnippet;
                result.Explanation = $"[Truncated] {result.Explanation}";
            }

            return result;
        }

        // Unreachable, but satisfies compiler
        return CreateFallback(violation);
    }

    internal static FixType ClassifyFixType(int lineCount) => lineCount switch
    {
        <= SmallSnippetMaxLines => FixType.SmallSnippet,
        <= MultiLineMaxLines => FixType.MultiLineSnippet,
        _ => FixType.FullFileRefactor
    };

    internal static int CountLines(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return 0;
        return code.Split('\n').Length;
    }

    internal static string TruncateSnippet(string code, int maxLines)
    {
        var lines = code.Split('\n');
        if (lines.Length <= maxLines) return code;
        return string.Join('\n', lines.Take(maxLines)) + "\n// ... truncated for brevity";
    }

    private static AutoFixResult ParseFixResponse(string response, AgentViolation violation)
    {
        var lines = response.Split('\n');
        var explanation = new List<string>();
        var code = new List<string>();
        var justification = (string?)null;
        var inCode = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                code.Add(line);
            }
            else if (line.TrimStart().StartsWith("Justification:", StringComparison.OrdinalIgnoreCase))
            {
                justification = line.TrimStart()["Justification:".Length..].Trim();
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                explanation.Add(line.Trim());
            }
        }

        return new AutoFixResult
        {
            OriginalFile = violation.File,
            Line = violation.Line,
            Issue = violation.Issue,
            CodeSnippet = string.Join('\n', code),
            Explanation = explanation.Count > 0
                ? string.Join(" ", explanation)
                : violation.SuggestedFix,
            FullRefactorJustification = justification
        };
    }

    private static AutoFixResult CreateFallback(AgentViolation violation) => new()
    {
        OriginalFile = violation.File,
        Line = violation.Line,
        Issue = violation.Issue,
        FixType = FixType.SmallSnippet,
        CodeSnippet = string.Empty,
        Explanation = violation.SuggestedFix
    };
}
