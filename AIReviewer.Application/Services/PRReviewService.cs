using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class PRReviewService : IPRReviewService
{
    private readonly IGitHubService _gitHubService;
    private readonly ISopProvider _sopProvider;
    private readonly IEnumerable<ICodeReviewAgent> _agents;
    private readonly IReviewAggregator _aggregator;
    private readonly ILogger<PRReviewService> _logger;

    public PRReviewService(
        IGitHubService gitHubService,
        ISopProvider sopProvider,
        IEnumerable<ICodeReviewAgent> agents,
        IReviewAggregator aggregator,
        ILogger<PRReviewService> logger)
    {
        _gitHubService = gitHubService;
        _sopProvider = sopProvider;
        _agents = agents;
        _aggregator = aggregator;
        _logger = logger;
    }

    public async Task<ReviewResult> ReviewPullRequestAsync(string repo, int prNumber)
    {
        _logger.LogInformation("Starting multi-agent review for PR #{PrNumber} in {Repo}", prNumber, repo);

        var diff = await _gitHubService.GetPullRequestDiffAsync(repo, prNumber);
        var sopContent = await _sopProvider.GetSopContentAsync();

        _logger.LogInformation("Executing {Count} review agents in parallel", _agents.Count());

        var agentTasks = _agents.Select(agent => ExecuteAgentSafely(agent, diff, sopContent));
        var agentResults = await Task.WhenAll(agentTasks);

        var unified = _aggregator.Aggregate(agentResults.Where(r => r is not null)!);

        var comment = FormatUnifiedComment(unified);
        await _gitHubService.PostPullRequestCommentAsync(repo, prNumber, comment);

        _logger.LogInformation("Multi-agent review posted for PR #{PrNumber}", prNumber);

        return new ReviewResult
        {
            Summary = unified.OverallSummary,
            Violations = unified.AllViolations
        };
    }

    private async Task<AgentReviewResult> ExecuteAgentSafely(
        ICodeReviewAgent agent, string diff, string sopContent)
    {
        try
        {
            return await agent.ReviewAsync(diff, sopContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Agent failed, continuing with other agents", agent.AgentName);
            return new AgentReviewResult
            {
                AgentName = agent.AgentName,
                Summary = $"Agent failed: {ex.Message}"
            };
        }
    }

    private static string FormatUnifiedComment(UnifiedReviewResult unified)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## AI Multi-Agent Review");
        sb.AppendLine();

        foreach (var agent in unified.AgentResults)
        {
            sb.AppendLine($"### {agent.AgentName} Findings");
            sb.AppendLine();

            if (agent.Violations.Count == 0)
            {
                sb.AppendLine("No issues found.");
            }
            else
            {
                foreach (var v in agent.Violations)
                {
                    sb.AppendLine($"- **{v.Severity}** `{v.File}:{v.Line}` — {v.Issue}");
                    sb.AppendLine($"  - Fix: {v.SuggestedFix}");
                }
            }

            sb.AppendLine();
        }

        var totalViolations = unified.AllViolations.Count;
        sb.AppendLine("---");
        sb.AppendLine($"**Total Violations:** {totalViolations}");

        if (totalViolations == 0)
            sb.AppendLine("All agents passed. Looks good!");

        return sb.ToString();
    }
}
