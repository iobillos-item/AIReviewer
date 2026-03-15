using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class ReviewCoordinator : IReviewCoordinator
{
    private readonly IGitHubService _gitHubService;
    private readonly IDiffChunker _diffChunker;
    private readonly ISopContextRetriever _sopContextRetriever;
    private readonly IEnumerable<ICodeReviewAgent> _agents;
    private readonly IReviewAggregator _aggregator;
    private readonly IReviewCommentFormatter _formatter;
    private readonly ILogger<ReviewCoordinator> _logger;

    public ReviewCoordinator(
        IGitHubService gitHubService,
        IDiffChunker diffChunker,
        ISopContextRetriever sopContextRetriever,
        IEnumerable<ICodeReviewAgent> agents,
        IReviewAggregator aggregator,
        IReviewCommentFormatter formatter,
        ILogger<ReviewCoordinator> logger)
    {
        _gitHubService = gitHubService;
        _diffChunker = diffChunker;
        _sopContextRetriever = sopContextRetriever;
        _agents = agents;
        _aggregator = aggregator;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task<UnifiedReviewResult> ReviewAsync(string repo, int prNumber)
    {
        _logger.LogInformation("Coordinator starting review for PR #{PrNumber} in {Repo}", prNumber, repo);

        var diff = await _gitHubService.GetPullRequestDiffAsync(repo, prNumber);
        var chunks = (await _diffChunker.ChunkAsync(diff)).ToList();
        _logger.LogInformation("Split diff into {Count} chunks", chunks.Count);

        var allResults = new List<AgentReviewResult>();

        foreach (var chunk in chunks)
        {
            var sopContext = await _sopContextRetriever.GetRelevantContextAsync(chunk.Content);

            // Agents handle their own errors internally — no wrapper needed
            var agentTasks = _agents.Select(agent => agent.ReviewAsync(chunk, sopContext));
            var chunkResults = await Task.WhenAll(agentTasks);
            allResults.AddRange(chunkResults);
        }

        var unified = _aggregator.Aggregate(allResults);
        _logger.LogInformation("Aggregated {Count} results with {Violations} violations",
            allResults.Count, unified.Violations.Count);

        var comment = _formatter.Format(unified);
        await _gitHubService.PostPullRequestCommentAsync(repo, prNumber, comment);

        return unified;
    }
}
