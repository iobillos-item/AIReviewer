namespace AIReviewer.Application.Interfaces;

public interface IPromptBuilder
{
    string BuildReviewPrompt(string sopContent, string prDiff);
    string BuildAgentPrompt(string agentType, string diff, string sopContent);
}
