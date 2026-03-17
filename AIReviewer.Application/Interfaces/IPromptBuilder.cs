using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IPromptBuilder
{
    string BuildReviewPrompt(string sopContent, string prDiff);
    string BuildAgentPrompt(string agentType, DiffChunk chunk, string sopContext);
    string BuildRouterPrompt(DiffChunk chunk, IEnumerable<string> availableAgents);
    string BuildMetaReviewPrompt(IEnumerable<AgentViolation> violations);
    string BuildAutoFixPrompt(AgentViolation violation, DiffChunk chunk);
}
