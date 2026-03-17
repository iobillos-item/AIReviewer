using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IGitHubReviewService
{
    Task PostInlineCommentAsync(string repo, int prNumber, string commitId, AgentViolation violation, AutoFixResult? fix);
    Task<string> GetPullRequestHeadShaAsync(string repo, int prNumber);
}
