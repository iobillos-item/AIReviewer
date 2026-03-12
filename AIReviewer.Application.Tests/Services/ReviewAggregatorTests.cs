using AIReviewer.Application.Services;
using AIReviewer.Domain.Entities;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class ReviewAggregatorTests
{
    private readonly ReviewAggregator _sut = new();

    [Fact]
    public void Aggregate_MultipleAgents_MergesAllViolations()
    {
        var results = new[]
        {
            new AgentReviewResult
            {
                AgentName = "Architecture",
                Summary = "Layer issues",
                Violations = [new ReviewViolation { AgentName = "Architecture", File = "A.cs", Issue = "Coupling" }]
            },
            new AgentReviewResult
            {
                AgentName = "Security",
                Summary = "Secrets found",
                Violations = [new ReviewViolation { AgentName = "Security", File = "B.cs", Issue = "Hardcoded key" }]
            }
        };

        var unified = _sut.Aggregate(results);

        Assert.Equal(2, unified.AllViolations.Count);
        Assert.Equal(2, unified.AgentResults.Count);
        Assert.Contains("[Architecture]", unified.OverallSummary);
        Assert.Contains("[Security]", unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_NoResults_ReturnsEmpty()
    {
        var unified = _sut.Aggregate([]);

        Assert.Empty(unified.AllViolations);
        Assert.Empty(unified.AgentResults);
        Assert.Equal(string.Empty, unified.OverallSummary);
    }

    [Fact]
    public void Aggregate_AgentWithNoViolations_IncludedInResults()
    {
        var results = new[]
        {
            new AgentReviewResult { AgentName = "Performance", Summary = "All good" }
        };

        var unified = _sut.Aggregate(results);

        Assert.Single(unified.AgentResults);
        Assert.Empty(unified.AllViolations);
        Assert.Contains("Performance", unified.OverallSummary);
    }
}
