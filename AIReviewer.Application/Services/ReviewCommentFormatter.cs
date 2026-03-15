using System.Text;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Services;

public class ReviewCommentFormatter : IReviewCommentFormatter
{
    public string Format(UnifiedReviewResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 🤖 AI Multi-Agent Code Review");
        sb.AppendLine();

        // Each agent now has one consolidated result (merged across all chunks)
        var agentGroups = result.AgentResults
            .Where(r => !r.HasError)
            .OrderBy(r => r.AgentName);

        foreach (var agent in agentGroups)
        {
            sb.AppendLine($"### {agent.AgentName} Findings");

            if (agent.Violations.Count == 0)
            {
                sb.AppendLine("✅ No issues found.");
            }
            else
            {
                // Group violations by file for readability
                var byFile = agent.Violations
                    .GroupBy(v => v.File)
                    .OrderBy(g => g.Key);

                foreach (var fileGroup in byFile)
                {
                    foreach (var v in fileGroup.OrderByDescending(v => v.Severity == "Critical" ? 4 : v.Severity == "Major" ? 3 : v.Severity == "Minor" ? 2 : 1))
                    {
                        sb.AppendLine($"- **{v.Severity}** `{v.File}:{v.Line}` — {v.Issue}");
                        if (!string.IsNullOrWhiteSpace(v.SuggestedFix))
                            sb.AppendLine($"  - Fix: {v.SuggestedFix}");
                    }
                }
            }

            sb.AppendLine($"_⏱ {agent.Duration.TotalSeconds:F1}s_");
            sb.AppendLine();
        }

        // Report failed agents
        var failed = result.AgentResults.Where(r => r.HasError).ToList();
        if (failed.Count > 0)
        {
            sb.AppendLine("### ⚠️ Agent Failures");
            foreach (var f in failed)
                sb.AppendLine($"- **{f.AgentName}**: {f.ErrorMessage}");
            sb.AppendLine();
        }

        sb.AppendLine("### Overall Summary");
        sb.AppendLine(result.OverallSummary);

        return sb.ToString();
    }
}
