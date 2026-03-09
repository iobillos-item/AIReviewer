namespace AIReviewer.Application.Interfaces;

public interface IPromptBuilder
{
    string BuildReviewPrompt(string sopContent, string prDiff);
}
