using AIReviewer.Application.Services;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class ResponseParserTests
{
    private readonly ResponseParser _sut = new();

    [Fact]
    public void ParseAgentResponse_WithViolations_ReturnsCorrectResult()
    {
        var response = """
            Summary
            Found a security issue.

            Violations
            File: Auth.cs
            Line: 15
            Issue: Hardcoded password
            Suggested Fix: Use configuration
            Severity: Critical
            """;

        var result = _sut.ParseAgentResponse(response, "Security");

        Assert.Equal("Security", result.AgentName);
        Assert.Equal("Found a security issue.", result.Summary);
        Assert.Single(result.Violations);
        Assert.Equal("Auth.cs", result.Violations[0].File);
        Assert.Equal(15, result.Violations[0].Line);
        Assert.Equal("Hardcoded password", result.Violations[0].Issue);
        Assert.Equal("Use configuration", result.Violations[0].SuggestedFix);
        Assert.Equal("Critical", result.Violations[0].Severity);
        Assert.Equal("Security", result.Violations[0].AgentName);
    }

    [Fact]
    public void ParseAgentResponse_NoViolations_ReturnsEmptyList()
    {
        var response = """
            Summary
            Code looks clean.

            Violations
            """;

        var result = _sut.ParseAgentResponse(response, "Architecture");

        Assert.Equal("Code looks clean.", result.Summary);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ParseAgentResponse_MultipleViolations_ParsesAll()
    {
        var response = """
            Summary
            Multiple issues.

            Violations
            File: A.cs
            Line: 1
            Issue: Issue one
            Suggested Fix: Fix one
            Severity: Critical
            File: B.cs
            Line: 2
            Issue: Issue two
            Suggested Fix: Fix two
            Severity: Minor
            """;

        var result = _sut.ParseAgentResponse(response, "Test");

        Assert.Equal(2, result.Violations.Count);
        Assert.Equal("A.cs", result.Violations[0].File);
        Assert.Equal("B.cs", result.Violations[1].File);
    }

    [Fact]
    public void ParseAgentResponse_EmptyResponse_ReturnsEmptyResult()
    {
        var result = _sut.ParseAgentResponse(string.Empty, "Agent");

        Assert.Equal(string.Empty, result.Summary);
        Assert.Empty(result.Violations);
        Assert.Equal("Agent", result.AgentName);
    }

    [Fact]
    public void ParseAgentResponse_MalformedResponse_HandlesGracefully()
    {
        var response = "This is just random text with no structure";

        var result = _sut.ParseAgentResponse(response, "Agent");

        Assert.Equal(string.Empty, result.Summary);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ParseAgentResponse_PartialViolation_DoesNotIncludeIncomplete()
    {
        var response = """
            Summary
            Partial data.

            Violations
            File: Test.cs
            Line: 5
            Issue: Missing something
            """;

        // No Severity line = violation not finalized
        var result = _sut.ParseAgentResponse(response, "Agent");

        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ParseLegacyResponse_WithViolations_ReturnsReviewResult()
    {
        var response = """
            Summary
            Found an issue.

            Violations
            File: Service.cs
            Line: 10
            Issue: Bad practice
            Suggested Fix: Fix it
            Severity: Major
            """;

        var result = _sut.ParseLegacyResponse(response);

        Assert.Equal("Found an issue.", result.Summary);
        Assert.Single(result.Violations);
        Assert.Equal("Service.cs", result.Violations[0].File);
        Assert.Equal("Major", result.Violations[0].Severity);
    }

    [Fact]
    public void ParseLegacyResponse_EmptyResponse_ReturnsEmptyResult()
    {
        var result = _sut.ParseLegacyResponse(string.Empty);

        Assert.Equal(string.Empty, result.Summary);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ParseAgentResponse_NonNumericLine_DefaultsToZero()
    {
        var response = """
            Summary
            Test.

            Violations
            File: Test.cs
            Line: abc
            Issue: Something
            Suggested Fix: Fix
            Severity: Info
            """;

        var result = _sut.ParseAgentResponse(response, "Agent");

        Assert.Single(result.Violations);
        Assert.Equal(0, result.Violations[0].Line);
    }

    [Fact]
    public void ParseAgentResponse_MultiLineSummary_JoinsWithSpace()
    {
        var response = """
            Summary
            Line one of summary.
            Line two of summary.

            Violations
            """;

        var result = _sut.ParseAgentResponse(response, "Agent");

        Assert.Equal("Line one of summary. Line two of summary.", result.Summary);
    }
}
