using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class AutoFixSuggestionGenerator : IAutoFixSuggestionGenerator
{
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
            var response = await _llmService.GetCompletionAsync(prompt);
            var result = ParseFixResponse(response, violation);

            _logger.LogInformation("Generated fix for {File}:{Line} ({Agent})",
                violation.File, violation.Line, violation.AgentName);

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
                SuggestedCode = string.Empty,
                Explanation = violation.SuggestedFix // fallback to agent's suggestion
            };
        }
    }

    private static AutoFixResult ParseFixResponse(string response, AgentViolation violation)
    {
        var lines = response.Split('\n');
        var explanation = new List<string>();
        var code = new List<string>();
        var inCode = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
                code.Add(line);
            else if (!string.IsNullOrWhiteSpace(line))
                explanation.Add(line.Trim());
        }

        return new AutoFixResult
        {
            OriginalFile = violation.File,
            Line = violation.Line,
            Issue = violation.Issue,
            SuggestedCode = string.Join('\n', code),
            Explanation = explanation.Count > 0
                ? string.Join(" ", explanation)
                : violation.SuggestedFix
        };
    }
}
