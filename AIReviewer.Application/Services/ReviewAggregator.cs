using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Services;

public class ReviewAggregator : IReviewAggregator
{
    public UnifiedReviewResult Aggregate(IEnumerable<AgentReviewResult> results)
    {
        var resultList = results.ToList();

        // Consolidate per-chunk results into one result per agent
        var consolidated = resultList
            .GroupBy(r => r.AgentName)
            .Select(g => new AgentReviewResult
            {
                AgentName = g.Key,
                Violations = g.Where(r => !r.HasError).SelectMany(r => r.Violations).ToList(),
                Duration = TimeSpan.FromMilliseconds(g.Sum(r => r.Duration.TotalMilliseconds)),
                HasError = g.All(r => r.HasError), // only mark failed if ALL chunks failed
                ErrorMessage = g.Any(r => r.HasError)
                    ? string.Join("; ", g.Where(r => r.HasError).Select(r => r.ErrorMessage))
                    : null,
                Summary = string.Join(" ", g.Where(r => !r.HasError).Select(r => r.Summary).Where(s => !string.IsNullOrWhiteSpace(s)))
            })
            .ToList();

        // Deduplicate: same file + line + issue from different agents = one violation
        var allViolations = consolidated
            .Where(r => !r.HasError)
            .SelectMany(r => r.Violations)
            .GroupBy(v => new { v.File, v.Line, v.Issue })
            .Select(g => g.OrderByDescending(v => SeverityRank(v.Severity)).First())
            .OrderByDescending(v => SeverityRank(v.Severity))
            .ToList();

        var agentCount = consolidated.Select(r => r.AgentName).Distinct().Count();
        var failedAgents = consolidated.Where(r => r.HasError).ToList();

        var summaryParts = new List<string>
        {
            $"Reviewed by {agentCount} agents.",
            $"{allViolations.Count} total violations found."
        };

        if (failedAgents.Count > 0)
        {
            summaryParts.Add(
                $"{failedAgents.Count} agent(s) failed: {string.Join(", ", failedAgents.Select(a => a.AgentName))}.");
        }

        var bySeverity = allViolations
            .GroupBy(v => v.Severity)
            .Select(g => $"{g.Count()} {g.Key}")
            .ToList();

        if (bySeverity.Count > 0)
            summaryParts.Add($"Breakdown: {string.Join(", ", bySeverity)}.");

        return new UnifiedReviewResult
        {
            OverallSummary = string.Join(" ", summaryParts),
            Violations = allViolations,
            AgentResults = consolidated
        };
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 4,
        "Major" => 3,
        "Minor" => 2,
        "Info" => 1,
        _ => 0
    };
}
