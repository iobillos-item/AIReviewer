using System.Text.RegularExpressions;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public abstract class BaseReviewAgent : ICodeReviewAgent
{
    private readonly ILLMService _llmService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILogger _logger;

    public abstract string AgentName { get; }
    protected abstract string AgentType { get; }

    protected BaseReviewAgent(ILLMService llmService, IPromptBuilder promptBuilder, ILogger logger)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<AgentReviewResult> ReviewAsync(string diff, string sopContent)
    {
        _logger.LogInformation("[{Agent}] Starting review", AgentName);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var prompt = _promptBuilder.BuildAgentPrompt(AgentType, diff, sopContent);
        var aiResponse = await _llmService.GetCompletionAsync(prompt);

        stopwatch.Stop();
        _logger.LogInformation("[{Agent}] Completed in {Duration}ms", AgentName, stopwatch.ElapsedMilliseconds);

        return ParseResponse(aiResponse);
    }

    private AgentReviewResult ParseResponse(string response)
    {
        var result = new AgentReviewResult { AgentName = AgentName };
        var lines = response.Split('\n', StringSplitOptions.TrimEntries);

        var summaryLines = new List<string>();
        var inSummary = false;
        var inViolations = false;
        ReviewViolation? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("Summary", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = true;
                inViolations = false;
                continue;
            }

            if (line.StartsWith("Violations", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = false;
                inViolations = true;
                continue;
            }

            if (inSummary && !string.IsNullOrWhiteSpace(line))
                summaryLines.Add(line);

            if (!inViolations) continue;

            if (line.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
            {
                current = new ReviewViolation { AgentName = AgentName, File = line[5..].Trim() };
            }
            else if (line.StartsWith("Line:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                if (int.TryParse(Regex.Match(line[5..].Trim(), @"\d+").Value, out var lineNum))
                    current.Line = lineNum;
            }
            else if (line.StartsWith("Issue:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                current.Issue = line[6..].Trim();
            }
            else if (line.StartsWith("Suggested Fix:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                current.SuggestedFix = line[14..].Trim();
            }
            else if (line.StartsWith("Severity:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                current.Severity = line[9..].Trim();
                result.Violations.Add(current);
                current = null;
            }
        }

        result.Summary = string.Join(" ", summaryLines);
        return result;
    }
}
