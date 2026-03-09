using AIReviewer.Application.Interfaces;

namespace AIReviewer.Infrastructure.Persistence;

public class PromptBuilder : IPromptBuilder
{
    public string BuildReviewPrompt(string sopContent, string prDiff)
    {
        return $"""
            You are a strict senior software architect.
            Follow these SOP rules strictly:

            {sopContent}

            Review the following PR diff:

            {prDiff}

            Return output in this format:

            Summary
            <your summary here>

            Violations
            File: <filename>
            Line: <line number>
            Issue: <description>
            Suggested Fix: <fix>
            Severity: <Critical|Major|Minor|Info>

            Be concise and actionable.
            """;
    }
}
