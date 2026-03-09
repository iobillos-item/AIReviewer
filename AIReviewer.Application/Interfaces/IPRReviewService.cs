using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IPRReviewService
{
    Task<ReviewResult> ReviewPullRequestAsync(string repo, int prNumber);
}
