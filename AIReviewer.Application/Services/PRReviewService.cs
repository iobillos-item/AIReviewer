using System.Text.RegularExpressions;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class PRReviewService : IPRReviewService
{
    private readonly IGitHubService _gitHubService;
    private readonly ILLMService _llmService;
    private readonly ISopProvider _sopProvider;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILogger<PRReviewService> _logger;

    public PRReviewService(
        IGitHubService gitHubService,
        ILLMService llmService,
        ISopProvider sopProvider,
        IPromptBuilder promptBuilder,
        ILogger<PRReviewService> logger)
    {
        _gitHubService = gitHubService;
        _llmService = llmService;
        _sopProvider = sopProvider;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<ReviewResult> ReviewPullRequestAsync(string repo, int prNumber)
    {
        _logger.LogInformation("Starting review for PR #{PrNumber} in {Repo}", prNumber, repo);

        var diff = await _gitHubService.GetPullRequestDiffAsync(repo, prNumber);
        _logger.LogInformation("PR diff fetched for PR #{PrNumber}", prNumber);

        var sopContent = await _sopProvider.GetSopContentAsync();
        var prompt = _promptBuilder.BuildReviewPrompt(sopContent, diff);

        _logger.LogInformation("Prompt sent to AI for PR #{PrNumber}", prNumber);
        var aiResponse = await _llmService.GetCompletionAsync(prompt);

        var result = ParseReviewResponse(aiResponse);

        var comment = FormatReviewComment(result);
        await _gitHubService.PostPullRequestCommentAsync(repo, prNumber, comment);
        _logger.LogInformation("Review comment posted for PR #{PrNumber}", prNumber);

        return result;
    }

    private static ReviewResult ParseReviewResponse(string response)
    {
        var result = new ReviewResult();
        var lines = response.Split('\n', StringSplitOptions.TrimEntries);

        var summaryLines = new List<string>();
        var violations = new List<ReviewViolation>();
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
            {
                summaryLines.Add(line);
            }

            if (!inViolations) continue;

            if (line.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
            {
                current = new ReviewViolation { File = line[5..].Trim() };
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
                violations.Add(current);
                current = null;
            }
        }

        result.Summary = string.Join(" ", summaryLines);
        result.Violations = violations;
        return result;
    }

    private static string FormatReviewComment(ReviewResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## AI PR Review");
        sb.AppendLine();
        sb.AppendLine($"**Summary:** {result.Summary}");
        sb.AppendLine();

        if (result.Violations.Count == 0)
        {
            sb.AppendLine("No violations found. Looks good!");
            return sb.ToString();
        }

        sb.AppendLine($"**Violations Found:** {result.Violations.Count}");
        sb.AppendLine();

        foreach (var v in result.Violations)
        {
            sb.AppendLine($"- **{v.Severity}** `{v.File}:{v.Line}` — {v.Issue}");
            sb.AppendLine($"  - Fix: {v.SuggestedFix}");
        }

        return sb.ToString();
    }
}
