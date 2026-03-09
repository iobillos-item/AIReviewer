using System.Text.Json;
using AIReviewer.Application.DTOs;
using AIReviewer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIReviewer.WebAPI.Controllers;

[ApiController]
[Route("api/github")]
public class WebhookController : ControllerBase
{
    private readonly IPRReviewService _reviewService;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IPRReviewService reviewService,
        IGitHubService gitHubService,
        ILogger<WebhookController> logger)
    {
        _reviewService = reviewService;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [Consumes("application/json", "application/x-www-form-urlencoded")]
    public async Task<IActionResult> HandleWebhook()
    {
        // Read raw body to avoid model binding issues with GitHub's Content-Type
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        _logger.LogInformation("Webhook received: event={EventType}", eventType);

        return eventType switch
        {
            "pull_request" => await HandlePullRequestEvent(body),
            "push" => await HandlePushEvent(body),
            _ => Ok(new { message = $"Event '{eventType}' ignored" })
        };
    }

    private async Task<IActionResult> HandlePullRequestEvent(string body)
    {
        var payload = JsonSerializer.Deserialize<PullRequestWebhookPayload>(body);
        if (payload is null)
            return BadRequest(new { error = "Invalid pull_request payload" });

        _logger.LogInformation("pull_request event: action={Action}, PR #{Number}",
            payload.Action, payload.PullRequest?.Number);

        if (payload.Action is not ("opened" or "synchronize"))
        {
            return Ok(new { message = $"pull_request action '{payload.Action}' ignored" });
        }

        var repo = payload.Repository?.FullName;
        var prNumber = payload.PullRequest?.Number;

        if (string.IsNullOrEmpty(repo) || prNumber is null or 0)
            return BadRequest(new { error = "Missing repository or PR number" });

        var result = await _reviewService.ReviewPullRequestAsync(repo, prNumber.Value);

        return Ok(new
        {
            message = "Review completed",
            summary = result.Summary,
            violationCount = result.Violations.Count
        });
    }

    private async Task<IActionResult> HandlePushEvent(string body)
    {
        var payload = JsonSerializer.Deserialize<PushWebhookPayload>(body);
        if (payload is null)
            return BadRequest(new { error = "Invalid push payload" });

        var repo = payload.Repository?.FullName;
        var commitSha = payload.After;

        _logger.LogInformation("push event: repo={Repo}, head={Sha}", repo, commitSha?[..7]);

        if (string.IsNullOrEmpty(repo) || string.IsNullOrEmpty(commitSha))
            return BadRequest(new { error = "Missing repository or commit SHA" });

        // Find PRs associated with this push's head commit
        var prNumbers = await _gitHubService.GetPullRequestsForCommitAsync(repo, commitSha);

        if (prNumbers.Count == 0)
        {
            _logger.LogInformation("No open PRs found for commit {Sha}, skipping review", commitSha[..7]);
            return Ok(new { message = "No associated PRs found for this push" });
        }

        var results = new List<object>();
        foreach (var prNumber in prNumbers)
        {
            _logger.LogInformation("Reviewing PR #{PrNumber} triggered by push", prNumber);
            var result = await _reviewService.ReviewPullRequestAsync(repo, prNumber);
            results.Add(new
            {
                prNumber,
                summary = result.Summary,
                violationCount = result.Violations.Count
            });
        }

        return Ok(new { message = "Push review completed", reviews = results });
    }
}
