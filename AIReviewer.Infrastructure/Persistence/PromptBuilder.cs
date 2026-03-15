using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;

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

    public string BuildAgentPrompt(string agentType, DiffChunk chunk, string sopContext)
    {
        var role = GetAgentRole(agentType);

        return $"""
            {role}

            Follow these rules strictly:
            {sopContext}

            Analyze this code diff from file "{chunk.FileName}" (lines {chunk.StartLine}-{chunk.EndLine}):

            {chunk.Content}

            Return output in this exact format:

            Summary
            <your summary here>

            Violations
            File: <filename>
            Line: <line number>
            Issue: <description>
            Suggested Fix: <fix>
            Severity: <Critical|Major|Minor|Info>

            If no violations found, return only the Summary section.
            Be concise and actionable.
            """;
    }

    private static string GetAgentRole(string agentType) => agentType switch
    {
        "Architecture" => """
            You are a senior software architect specializing in Clean Architecture.
            Detect: layer coupling, controller business logic, dependency inversion violations, domain leakage.
            """,
        "Security" => """
            You are a senior security engineer.
            Detect: SQL injection risks, hardcoded secrets, unsafe APIs, authentication issues, authorization issues.
            """,
        "Performance" => """
            You are a senior performance engineer.
            Detect: inefficient loops, blocking async calls, memory misuse, expensive queries.
            """,
        "TestCoverage" => """
            You are a senior QA engineer specializing in test coverage.
            Detect: missing unit tests for new services, missing edge case tests, insufficient coverage.
            """,
        "Dependency" => """
            You are a senior dependency and supply chain security analyst.
            Detect: outdated dependencies, vulnerable packages, large dependency additions.
            """,
        _ => "You are a senior code reviewer."
    };
}
