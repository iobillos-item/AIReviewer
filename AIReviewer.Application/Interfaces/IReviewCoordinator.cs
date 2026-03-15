using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IReviewCoordinator
{
    Task<UnifiedReviewResult> ReviewAsync(string repo, int prNumber);
}
