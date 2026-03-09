using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IGitHubService
{
    Task<PullRequest> GetPullRequestAsync(string repo, int prNumber);
    Task<string> GetPullRequestDiffAsync(string repo, int prNumber);
    Task PostPullRequestCommentAsync(string repo, int prNumber, string comment);
    Task<List<int>> GetPullRequestsForCommitAsync(string repo, string commitSha);
}

