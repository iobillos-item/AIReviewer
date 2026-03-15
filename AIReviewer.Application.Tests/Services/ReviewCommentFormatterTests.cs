using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class ReviewCommentFormatterTests
{
    private readonly ReviewCommentFormatter _sut = new();

    [Fact]
    public void Format_WithViolations_IncludesAgentSections()
    {
        var result = new UnifiedReviewResult
        {
            OverallSummary = "2 violations found.",
            AgentResults = new List<AgentReviewResult>
            {
                new()
                {
                    AgentName = "Security",
                    Violations = new List<AgentViolation>
                    {
                        new() { Severity = "Critical", File = "Auth.cs", Line = 5, Issue = "Hardcoded secret", SuggestedFix = "Use config" }
                    },
                    Duration = TimeSpan.FromSeconds(1.5)
                }
            }
        };

        var comment = _sut.Format(result);

        Assert.Contains("AI Multi-Agent Code Review", comment);
        Assert.Contains("Security", comment);
        Assert.Contains("Auth.cs:5", comment);
        Assert.Contains("Hardcoded secret", comment);
        Assert.Contains("Use config", comment);
        Assert.Contains("1.5s", comment);
    }

    [Fact]
    public void Format_NoViolations_ShowsNoIssuesFound()
    {
        var result = new UnifiedReviewResult
        {
            OverallSummary = "All clean.",
            AgentResults = new List<AgentReviewResult>
            {
                new() { AgentName = "Architecture", Violations = new(), Duration = TimeSpan.FromSeconds(0.5) }
            }
        };

        var comment = _sut.Format(result);

        Assert.Contains("No issues found", comment);
    }

    [Fact]
    public void Format_FailedAgent_ReportsFailure()
    {
        var result = new UnifiedReviewResult
        {
            OverallSummary = "Partial review.",
            AgentResults = new List<AgentReviewResult>
            {
                new() { AgentName = "Security", HasError = true, ErrorMessage = "Timeout" }
            }
        };

        var comment = _sut.Format(result);

        Assert.Contains("Agent Failures", comment);
        Assert.Contains("Security", comment);
        Assert.Contains("Timeout", comment);
    }

    [Fact]
    public void Format_MixedResults_ShowsBothSuccessAndFailure()
    {
        var result = new UnifiedReviewResult
        {
            OverallSummary = "Mixed.",
            AgentResults = new List<AgentReviewResult>
            {
                new() { AgentName = "Architecture", Violations = new(), Duration = TimeSpan.FromSeconds(1) },
                new() { AgentName = "Security", HasError = true, ErrorMessage = "Error" }
            }
        };

        var comment = _sut.Format(result);

        Assert.Contains("Architecture", comment);
        Assert.Contains("No issues found", comment);
        Assert.Contains("Agent Failures", comment);
        Assert.Contains("Security", comment);
    }

    [Fact]
    public void Format_IncludesOverallSummary()
    {
        var result = new UnifiedReviewResult
        {
            OverallSummary = "Reviewed by 5 agents. 3 total violations found.",
            AgentResults = new()
        };

        var comment = _sut.Format(result);

        Assert.Contains("Overall Summary", comment);
        Assert.Contains("Reviewed by 5 agents", comment);
    }

    [Fact]
    public void Format_ViolationWithoutSuggestedFix_OmitsFixLine()
    {
        var result = new UnifiedReviewResult
        {
            OverallSummary = "Done.",
            AgentResults = new List<AgentReviewResult>
            {
                new()
                {
                    AgentName = "Performance",
                    Violations = new List<AgentViolation>
                    {
                        new() { Severity = "Minor", File = "X.cs", Line = 1, Issue = "Slow", SuggestedFix = "" }
                    },
                    Duration = TimeSpan.FromSeconds(0.3)
                }
            }
        };

        var comment = _sut.Format(result);

        Assert.Contains("Slow", comment);
        Assert.DoesNotContain("Fix:", comment);
    }
}
