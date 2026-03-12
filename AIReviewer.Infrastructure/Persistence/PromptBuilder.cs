using AIReviewer.Application.Interfaces;

namespace AIReviewer.Infrastructure.Persistence;

public class PromptBuilder : IPromptBuilder
{
    private static readonly Dictionary<string, string> AgentPromptTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Architecture"] = """
            You are a senior software architect.
            Analyze this PR diff for violations of clean architecture, layering, and SOLID principles.
            Focus on: layer coupling, controller business logic, dependency inversion violations, domain leakage.
            """,
        ["Security"] = """
            You are a senior application security engineer.
            Identify security vulnerabilities in this PR diff.
            Focus on: SQL injection, secrets in code, unsafe deserialization, authentication flaws, authorization issues, sensitive data logging.
            """,
        ["Performance"] = """
            You are a performance engineer.
            Detect inefficient code patterns and performance risks in this PR diff.
            Focus on: inefficient loops, N+1 queries, memory misuse, blocking async calls, expensive operations.
            """,
        ["TestCoverage"] = """
            You are a senior QA engineer specializing in test coverage analysis.
            Analyze this PR diff and identify code that lacks adequate test coverage.
            Focus on: untested public methods, missing edge case tests, untested error paths, complex logic without unit tests, missing integration tests for critical flows.
            """
    };

    public string BuildReviewPrompt(string sopContent, string prDiff)
    {
        return BuildAgentPrompt("Architecture", prDiff, sopContent);
    }

    public string BuildAgentPrompt(string agentType, string diff, string sopContent)
    {
        var persona = AgentPromptTemplates.GetValueOrDefault(agentType,
            "You are a strict senior software architect.");

        return $"""
            {persona}

            Follow these SOP rules strictly:

            {sopContent}

            Review the following PR diff:

            {diff}

            Return output in this exact format:

            Summary
            <your summary here>

            Violations
            File: <filename>
            Line: <line number>
            Issue: <description>
            Suggested Fix: <fix>
            Severity: <Critical|Major|Minor|Info>

            If no violations found, return Summary with your assessment and an empty Violations section.
            Be concise and actionable.
            """;
    }
}
