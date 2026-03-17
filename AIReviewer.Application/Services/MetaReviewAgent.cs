using System.Text.Json;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class MetaReviewAgent : IMetaReviewAgent
{
    private readonly ILLMService _llmService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILogger<MetaReviewAgent> _logger;

    public MetaReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        ILogger<MetaReviewAgent> logger)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<IEnumerable<AgentViolation>> ValidateAsync(IEnumerable<AgentViolation> violations)
    {
        var violationList = violations.ToList();
        if (violationList.Count == 0)
            return violationList;

        try
        {
            var prompt = _promptBuilder.BuildMetaReviewPrompt(violationList);
            var response = await _llmService.GetCompletionAsync(prompt);
            var validated = ParseMetaResponse(response, violationList);

            _logger.LogInformation("MetaReview: {Before} violations in, {After} validated out",
                violationList.Count, validated.Count);

            return validated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MetaReview failed, returning original violations");
            return violationList;
        }
    }

    private static List<AgentViolation> ParseMetaResponse(string response, List<AgentViolation> original)
    {
        // The LLM returns a JSON array of indices to KEEP, or "keep_all"
        var trimmed = response.Trim();

        if (trimmed.Contains("keep_all", StringComparison.OrdinalIgnoreCase))
            return original;

        try
        {
            // Extract JSON array from response
            var start = trimmed.IndexOf('[');
            var end = trimmed.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                var jsonArray = trimmed[start..(end + 1)];
                var indices = JsonSerializer.Deserialize<List<int>>(jsonArray);
                if (indices is not null)
                {
                    return indices
                        .Where(i => i >= 0 && i < original.Count)
                        .Select(i => original[i])
                        .ToList();
                }
            }
        }
        catch
        {
            // If parsing fails, return all — safe fallback
        }

        return original;
    }
}
