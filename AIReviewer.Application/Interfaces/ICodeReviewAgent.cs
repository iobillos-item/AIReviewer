using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface ICodeReviewAgent
{
    string AgentName { get; }
    Task<AgentReviewResult> ReviewAsync(string diff, string sopContent);
}
