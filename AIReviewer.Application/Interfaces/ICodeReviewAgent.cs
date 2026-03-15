using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface ICodeReviewAgent
{
    string AgentName { get; }
    Task<AgentReviewResult> ReviewAsync(DiffChunk chunk, string sopContext);
}
