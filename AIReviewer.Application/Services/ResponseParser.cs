using System.Text.RegularExpressions;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Services;

public class ResponseParser : IResponseParser
{
    public AgentReviewResult ParseAgentResponse(string response, string agentName)
    {
        var (summary, violations) = Parse(response);

        return new AgentReviewResult
        {
            AgentName = agentName,
            Summary = summary,
            Violations = violations.Select(v => new AgentViolation
            {
                AgentName = agentName,
                File = v.File,
                Line = v.Line,
                Issue = v.Issue,
                SuggestedFix = v.SuggestedFix,
                Severity = v.Severity
            }).ToList()
        };
    }

    public ReviewResult ParseLegacyResponse(string response)
    {
        var (summary, violations) = Parse(response);

        return new ReviewResult
        {
            Summary = summary,
            Violations = violations.Select(v => new ReviewViolation
            {
                File = v.File,
                Line = v.Line,
                Issue = v.Issue,
                SuggestedFix = v.SuggestedFix,
                Severity = v.Severity
            }).ToList()
        };
    }

    private static (string Summary, List<ParsedViolation> Violations) Parse(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.TrimEntries);
        var summaryLines = new List<string>();
        var violations = new List<ParsedViolation>();
        var inSummary = false;
        var inViolations = false;
        ParsedViolation? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("Summary", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = true;
                inViolations = false;
                continue;
            }

            if (line.StartsWith("Violations", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = false;
                inViolations = true;
                continue;
            }

            if (inSummary && !string.IsNullOrWhiteSpace(line))
                summaryLines.Add(line);

            if (!inViolations) continue;

            if (line.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
            {
                current = new ParsedViolation { File = line[5..].Trim() };
            }
            else if (line.StartsWith("Line:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                if (int.TryParse(Regex.Match(line[5..].Trim(), @"\d+").Value, out var lineNum))
                    current.Line = lineNum;
            }
            else if (line.StartsWith("Issue:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                current.Issue = line[6..].Trim();
            }
            else if (line.StartsWith("Suggested Fix:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                current.SuggestedFix = line[14..].Trim();
            }
            else if (line.StartsWith("Severity:", StringComparison.OrdinalIgnoreCase) && current != null)
            {
                current.Severity = line[9..].Trim();
                violations.Add(current);
                current = null;
            }
        }

        return (string.Join(" ", summaryLines), violations);
    }

    private sealed class ParsedViolation
    {
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }
        public string Issue { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}
