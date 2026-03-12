using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Services;

public class ReviewAggregator : IReviewAggregator
{
    public UnifiedReviewResult Aggregate(IEnumerable<AgentReviewResult> agentResults)
    {
        var results = agentResults.ToList();

        var summaryParts = results
            .Where(r => !string.IsNullOrWhiteSpace(r.Summary))
            .Select(r => $"[{r.AgentName}] {r.Summary}");

        return new UnifiedReviewResult
        {
            OverallSummary = string.Join("\n", summaryParts),
            AgentResults = results
        };
    }
}
