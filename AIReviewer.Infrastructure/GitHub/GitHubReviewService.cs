using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Infrastructure.GitHub;

public class GitHubReviewService : IGitHubReviewService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReviewService> _logger;

    public GitHubReviewService(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubReviewService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var token = configuration["GitHub:AccessToken"];
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AIReviewer", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<string> GetPullRequestHeadShaAsync(string repo, int prNumber)
    {
        var response = await _httpClient.GetAsync($"repos/{repo}/pulls/{prNumber}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("head")
            .GetProperty("sha")
            .GetString() ?? string.Empty;
    }

    public async Task PostInlineCommentAsync(
        string repo, int prNumber, string commitId,
        AgentViolation violation, AutoFixResult? fix)
    {
        var body = FormatInlineComment(violation, fix);

        var payload = new
        {
            body,
            commit_id = commitId,
            path = violation.File,
            line = violation.Line,
            side = "RIGHT"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(
                $"repos/{repo}/pulls/{prNumber}/comments", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Inline comment failed for {File}:{Line} (HTTP {Status}): {Error}",
                    violation.File, violation.Line, (int)response.StatusCode, errorBody);
                return;
            }

            _logger.LogInformation("Inline comment posted: {File}:{Line}", violation.File, violation.Line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post inline comment for {File}:{Line}",
                violation.File, violation.Line);
        }
    }

    private static string FormatInlineComment(AgentViolation violation, AutoFixResult? fix)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"⚠️ **{violation.AgentName} Agent** — {violation.Severity}");
        sb.AppendLine();
        sb.AppendLine(violation.Issue);

        if (fix is not null && !string.IsNullOrWhiteSpace(fix.SuggestedCode))
        {
            sb.AppendLine();
            sb.AppendLine("**Suggested Fix:**");
            sb.AppendLine(fix.Explanation);
            sb.AppendLine();
            sb.AppendLine("```suggestion");
            sb.AppendLine(fix.SuggestedCode);
            sb.AppendLine("```");
        }
        else if (!string.IsNullOrWhiteSpace(violation.SuggestedFix))
        {
            sb.AppendLine();
            sb.AppendLine($"**Suggested Fix:** {violation.SuggestedFix}");
        }

        return sb.ToString();
    }
}
