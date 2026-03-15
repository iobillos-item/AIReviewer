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
    private readonly IResponseParser _responseParser;
    private readonly ILogger<PRReviewService> _logger;

    public PRReviewService(
        IGitHubService gitHubService,
        ILLMService llmService,
        ISopProvider sopProvider,
        IPromptBuilder promptBuilder,
        IResponseParser responseParser,
        ILogger<PRReviewService> logger)
    {
        _gitHubService = gitHubService;
        _llmService = llmService;
        _sopProvider = sopProvider;
        _promptBuilder = promptBuilder;
        _responseParser = responseParser;
        _logger = logger;
    }

    public async Task<ReviewResult> ReviewPullRequestAsync(string repo, int prNumber)
    {
        _logger.LogInformation("Starting review for PR #{PrNumber} in {Repo}", prNumber, repo);

        var diff = await _gitHubService.GetPullRequestDiffAsync(repo, prNumber);
        var sopContent = await _sopProvider.GetSopContentAsync();
        var prompt = _promptBuilder.BuildReviewPrompt(sopContent, diff);

        var aiResponse = await _llmService.GetCompletionAsync(prompt);
        var result = _responseParser.ParseLegacyResponse(aiResponse);

        var comment = FormatReviewComment(result);
        await _gitHubService.PostPullRequestCommentAsync(repo, prNumber, comment);

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
