using System.Text;
using System.Text.Json;
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

    public string BuildRouterPrompt(DiffChunk chunk, IEnumerable<string> availableAgents)
    {
        var agentList = string.Join(", ", availableAgents);

        return $"""
            You are a diff routing agent. Your job is to determine which review agents should analyze a given code diff.
            
            Available agents: {agentList}
            
            File: {chunk.FileName}
            Diff content:
            {chunk.Content}
            
            Based on the file type and content, list ONLY the agent names that are relevant.
            For example:
            - A controller file needs Architecture and Security, but probably not Dependency.
            - A .csproj file needs Dependency, but not Performance.
            - A test file needs TestCoverage, but not Security.
            
            Return ONLY the relevant agent names, one per line. No explanations.
            """;
    }

    public string BuildMetaReviewPrompt(IEnumerable<AgentViolation> violations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a meta-review agent. Your job is to validate code review findings.");
        sb.AppendLine("Remove false positives, merge duplicates, and verify severity levels.");
        sb.AppendLine();
        sb.AppendLine("Here are the violations found by review agents:");
        sb.AppendLine();

        var list = violations.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var v = list[i];
            sb.AppendLine($"[{i}] Agent: {v.AgentName} | File: {v.File}:{v.Line} | Severity: {v.Severity}");
            sb.AppendLine($"    Issue: {v.Issue}");
            sb.AppendLine($"    Fix: {v.SuggestedFix}");
            sb.AppendLine();
        }

        sb.AppendLine("Rules for validation:");
        sb.AppendLine("- Remove findings that are false positives (e.g., flagging EF Core LINQ as SQL injection).");
        sb.AppendLine("- Remove duplicate findings that describe the same issue on the same file/line.");
        sb.AppendLine("- Keep all legitimate findings.");
        sb.AppendLine();
        sb.AppendLine("Return a JSON array of indices to KEEP. Example: [0, 2, 5]");
        sb.AppendLine("If all findings are valid, return: keep_all");

        return sb.ToString();
    }

    public string BuildAutoFixPrompt(AgentViolation violation, DiffChunk chunk)
    {
        return $"""
            You are a senior developer generating a code fix suggestion.
            
            File: {violation.File}
            Line: {violation.Line}
            Issue: {violation.Issue}
            Agent: {violation.AgentName}
            Severity: {violation.Severity}
            
            Original code context:
            {chunk.Content}
            
            Generate a concise fix. Include:
            1. A brief explanation of what to change and why.
            2. The corrected code in a fenced code block (```).
            
            Keep the fix minimal and focused on the specific issue.
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
