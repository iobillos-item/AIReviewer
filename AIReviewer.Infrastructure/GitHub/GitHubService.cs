using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Infrastructure.GitHub;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var token = configuration["GitHub:AccessToken"];
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AIReviewer", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<PullRequest> GetPullRequestAsync(string repo, int prNumber)
    {
        var response = await _httpClient.GetAsync($"repos/{repo}/pulls/{prNumber}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new PullRequest
        {
            Id = root.GetProperty("id").GetInt32(),
            Repository = repo,
            Number = prNumber,
            Title = root.GetProperty("title").GetString() ?? string.Empty
        };
    }

    public async Task<string> GetPullRequestDiffAsync(string repo, int prNumber)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{repo}/pulls/{prNumber}");
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task PostPullRequestCommentAsync(string repo, int prNumber, string comment)
    {
        var payload = JsonSerializer.Serialize(new { body = comment });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"repos/{repo}/issues/{prNumber}/comments", content);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Comment posted to PR #{PrNumber} in {Repo}", prNumber, repo);
    }
    public async Task<List<int>> GetPullRequestsForCommitAsync(string repo, string commitSha)
    {
        var response = await _httpClient.GetAsync($"repos/{repo}/commits/{commitSha}/pulls");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var prNumbers = new List<int>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            prNumbers.Add(element.GetProperty("number").GetInt32());
        }

        _logger.LogInformation("Found {Count} PRs associated with commit {Sha} in {Repo}", prNumbers.Count, commitSha[..7], repo);
        return prNumbers;
    }
}
