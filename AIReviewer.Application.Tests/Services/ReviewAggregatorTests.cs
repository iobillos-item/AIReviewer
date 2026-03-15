using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class ReviewAggregatorTests
{
    private readonly ReviewAggregator _sut = new();

    [Fact]
    public void Aggregate_MultipleAgents_CombinesViolations()
    {
        var results = new List<AgentReviewResult>
        {
            new()
            {
                AgentName = "Security",
                Violations = new() { new() { Severity = "Critical", Issue = "SQL injection", File = "A.cs", Line = 1 } }
            },
            new()
            {
                AgentName = "Performance",
                Violations = new() { new() { Severity = "Minor", Issue = "Slow loop", File = "B.cs", Line = 2 } }
            }
        };

        var unified = _sut.Aggregate(results);

        Assert.Equal(2, unified.Violations.Count);
        Assert.Equal("Critical", unified.Violations[0].Severity);
        Assert.Contains("2 total violations", unified.OverallSummary);
        Assert.Contains("Reviewed by 2 agents", unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_FailedAgent_ReportedInSummary()
    {
        // Agent is only "failed" if ALL its chunks failed
        var results = new List<AgentReviewResult>
        {
            new() { AgentName = "Architecture", Violations = new() },
            new() { AgentName = "Security", HasError = true, ErrorMessage = "Timeout" }
        };

        var unified = _sut.Aggregate(results);

        Assert.Contains("1 agent(s) failed", unified.OverallSummary);
        Assert.Contains("Security", unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_NoViolations_ReturnsCleanSummary()
    {
        var results = new List<AgentReviewResult>
        {
            new() { AgentName = "Architecture", Violations = new() },
            new() { AgentName = "Security", Violations = new() }
        };

        var unified = _sut.Aggregate(results);

        Assert.Empty(unified.Violations);
        Assert.Contains("0 total violations", unified.OverallSummary);
        Assert.Contains("Reviewed by 2 agents", unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_SortsBySeverity_CriticalFirst()
    {
        var results = new List<AgentReviewResult>
        {
            new()
            {
                AgentName = "A",
                Violations = new()
                {
                    new() { Severity = "Info", Issue = "Info issue", File = "A.cs", Line = 1 },
                    new() { Severity = "Critical", Issue = "Critical issue", File = "B.cs", Line = 2 },
                    new() { Severity = "Major", Issue = "Major issue", File = "C.cs", Line = 3 }
                }
            }
        };

        var unified = _sut.Aggregate(results);

        Assert.Equal("Critical", unified.Violations[0].Severity);
        Assert.Equal("Major", unified.Violations[1].Severity);
        Assert.Equal("Info", unified.Violations[2].Severity);
    }

    [Fact]
    public void Aggregate_FailedAgentViolations_ExcludedFromList()
    {
        var results = new List<AgentReviewResult>
        {
            new()
            {
                AgentName = "Failed",
                HasError = true,
                Violations = new() { new() { Severity = "Critical", Issue = "Should not appear", File = "X.cs", Line = 1 } }
            },
            new()
            {
                AgentName = "Good",
                Violations = new() { new() { Severity = "Minor", Issue = "Real issue", File = "Y.cs", Line = 2 } }
            }
        };

        var unified = _sut.Aggregate(results);

        Assert.Single(unified.Violations);
        Assert.Equal("Real issue", unified.Violations[0].Issue);
    }

    [Fact]
    public void Aggregate_EmptyInput_ReturnsEmptyResult()
    {
        var unified = _sut.Aggregate(new List<AgentReviewResult>());

        Assert.Empty(unified.Violations);
        Assert.Contains("0 total violations", unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_IncludesSeverityBreakdown()
    {
        var results = new List<AgentReviewResult>
        {
            new()
            {
                AgentName = "A",
                Violations = new()
                {
                    new() { Severity = "Critical", Issue = "Issue A", File = "A.cs", Line = 1 },
                    new() { Severity = "Critical", Issue = "Issue B", File = "B.cs", Line = 2 },
                    new() { Severity = "Minor", Issue = "Issue C", File = "C.cs", Line = 3 }
                }
            }
        };

        var unified = _sut.Aggregate(results);

        Assert.Contains("2 Critical", unified.OverallSummary);
        Assert.Contains("1 Minor", unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_ConsolidatesByAgentName()
    {
        // Simulates 2 chunks reviewed by the same 2 agents
        var results = new List<AgentReviewResult>
        {
            new() { AgentName = "Architecture", Violations = new() { new() { Severity = "Minor", Issue = "Issue 1", File = "A.cs", Line = 1 } } },
            new() { AgentName = "Architecture", Violations = new() { new() { Severity = "Major", Issue = "Issue 2", File = "B.cs", Line = 5 } } },
            new() { AgentName = "Security", Violations = new() }
        };

        var unified = _sut.Aggregate(results);

        // Should consolidate to 2 agent results, not 3
        Assert.Equal(2, unified.AgentResults.Count);
        var arch = unified.AgentResults.First(r => r.AgentName == "Architecture");
        Assert.Equal(2, arch.Violations.Count);
    }

    [Fact]
    public void Aggregate_DeduplicatesSameViolation()
    {
        var results = new List<AgentReviewResult>
        {
            new()
            {
                AgentName = "Security",
                Violations = new() { new() { Severity = "Critical", Issue = "Hardcoded secret", File = "Config.cs", Line = 3 } }
            },
            new()
            {
                AgentName = "Architecture",
                Violations = new() { new() { Severity = "Major", Issue = "Hardcoded secret", File = "Config.cs", Line = 3 } }
            }
        };

        var unified = _sut.Aggregate(results);

        // Same file + line + issue = deduplicated to 1, keeping highest severity
        Assert.Single(unified.Violations);
        Assert.Equal("Critical", unified.Violations[0].Severity);
    }
}
