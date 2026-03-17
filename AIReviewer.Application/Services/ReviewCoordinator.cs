using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class ReviewCoordinator : IReviewCoordinator
{
    private readonly IGitHubService _gitHubService;
    private readonly IGitHubReviewService _reviewService;
    private readonly IDiffChunker _diffChunker;
    private readonly IDiffRouterAgent _router;
    private readonly ISopContextRetriever _sopContextRetriever;
    private readonly IEnumerable<ICodeReviewAgent> _agents;
    private readonly IMetaReviewAgent _metaReviewer;
    private readonly IAutoFixSuggestionGenerator _fixGenerator;
    private readonly IReviewAggregator _aggregator;
    private readonly IReviewCommentFormatter _formatter;
    private readonly ILogger<ReviewCoordinator> _logger;

    public ReviewCoordinator(
        IGitHubService gitHubService,
        IGitHubReviewService reviewService,
        IDiffChunker diffChunker,
        IDiffRouterAgent router,
        ISopContextRetriever sopContextRetriever,
        IEnumerable<ICodeReviewAgent> agents,
        IMetaReviewAgent metaReviewer,
        IAutoFixSuggestionGenerator fixGenerator,
        IReviewAggregator aggregator,
        IReviewCommentFormatter formatter,
        ILogger<ReviewCoordinator> logger)
    {
        _gitHubService = gitHubService;
        _reviewService = reviewService;
        _diffChunker = diffChunker;
        _router = router;
        _sopContextRetriever = sopContextRetriever;
        _agents = agents;
        _metaReviewer = metaReviewer;
        _fixGenerator = fixGenerator;
        _aggregator = aggregator;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task<UnifiedReviewResult> ReviewAsync(string repo, int prNumber)
    {
        _logger.LogInformation("Level-5 review starting for PR #{PrNumber} in {Repo}", prNumber, repo);

        // Step 1: Fetch diff
        var diff = await _gitHubService.GetPullRequestDiffAsync(repo, prNumber);

        // Step 2: Chunk
        var chunks = (await _diffChunker.ChunkAsync(diff)).ToList();
        _logger.LogInformation("Split diff into {Count} chunks", chunks.Count);

        // Step 3: Route chunks to relevant agents
        var routedChunks = (await _router.RouteAsync(chunks)).ToList();
        _logger.LogInformation("Routed {Count} chunks to targeted agents", routedChunks.Count);

        // Step 4-5: Execute routed agents in parallel with SOP context
        var agentLookup = _agents.ToDictionary(a => a.AgentName, StringComparer.OrdinalIgnoreCase);
        var allResults = new List<AgentReviewResult>();

        foreach (var routed in routedChunks)
        {
            var sopContext = await _sopContextRetriever.GetRelevantContextAsync(routed.Chunk.Content);

            var tasks = routed.AssignedAgents
                .Where(name => agentLookup.ContainsKey(name))
                .Select(name => agentLookup[name].ReviewAsync(routed.Chunk, sopContext));

            var chunkResults = await Task.WhenAll(tasks);
            allResults.AddRange(chunkResults);
        }

        // Step 6: Aggregate raw results
        var rawUnified = _aggregator.Aggregate(allResults);
        _logger.LogInformation("Raw review: {Count} violations from {Agents} agents",
            rawUnified.Violations.Count, rawUnified.AgentResults.Count);

        // Step 7: Meta-review validation (remove false positives, deduplicate)
        var validatedViolations = (await _metaReviewer.ValidateAsync(rawUnified.Violations)).ToList();
        _logger.LogInformation("MetaReview: {Before} → {After} violations",
            rawUnified.Violations.Count, validatedViolations.Count);

        // Step 8: Generate fixes for Critical/Major violations
        var chunkLookup = chunks.ToDictionary(c => c.FileName, StringComparer.OrdinalIgnoreCase);
        var fixes = new Dictionary<AgentViolation, AutoFixResult>();

        var fixTasks = validatedViolations
            .Where(v => v.Severity is "Critical" or "Major")
            .Select(async v =>
            {
                var chunk = chunkLookup.GetValueOrDefault(v.File) ?? new DiffChunk { FileName = v.File };
                var fix = await _fixGenerator.GenerateFixAsync(v, chunk);
                return (Violation: v, Fix: fix);
            });

        foreach (var result in await Task.WhenAll(fixTasks))
            fixes[result.Violation] = result.Fix;

        // Step 9: Build final unified result with validated violations
        rawUnified.Violations = validatedViolations;

        // Step 10: Post summary comment
        var comment = _formatter.Format(rawUnified);
        await _gitHubService.PostPullRequestCommentAsync(repo, prNumber, comment);

        // Step 11: Post inline comments on the PR
        var headSha = await _reviewService.GetPullRequestHeadShaAsync(repo, prNumber);

        var inlineTasks = validatedViolations.Select(v =>
            _reviewService.PostInlineCommentAsync(repo, prNumber, headSha, v, fixes.GetValueOrDefault(v)));

        await Task.WhenAll(inlineTasks);
        _logger.LogInformation("Posted {Count} inline comments to PR #{PrNumber}",
            validatedViolations.Count, prNumber);

        return rawUnified;
    }
}
