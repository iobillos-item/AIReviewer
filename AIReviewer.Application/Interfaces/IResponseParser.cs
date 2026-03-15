using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IResponseParser
{
    AgentReviewResult ParseAgentResponse(string response, string agentName);
    ReviewResult ParseLegacyResponse(string response);
}
