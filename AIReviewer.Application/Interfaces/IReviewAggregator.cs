using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IReviewAggregator
{
    UnifiedReviewResult Aggregate(IEnumerable<AgentReviewResult> results);
}
