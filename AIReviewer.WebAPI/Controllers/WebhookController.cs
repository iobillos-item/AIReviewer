using System.Text.Json;
using System.Web;
using AIReviewer.Application.DTOs;
using AIReviewer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIReviewer.WebAPI.Controllers;

[ApiController]
[Route("api/github")]
public class WebhookController : ControllerBase
{
    private readonly IReviewCoordinator _coordinator;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(     
        IReviewCoordinator coordinator,
        IGitHubService gitHubService,
        ILogger<WebhookController> logger)
    {     
        _coordinator = coordinator;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        // Enable buffering so the body stream can be re-read if middleware/content negotiation touched it
        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        // GitHub may send webhooks as form-urlencoded with a "payload" field
        body = ExtractJsonBody(body);

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

        var result = await _coordinator.ReviewAsync(repo, prNumber.Value);

        return Ok(new
        {
            message = "Multi-agent review completed",
            summary = result.OverallSummary,
            violationCount = result.Violations.Count,
            agentCount = result.AgentResults.Count
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
            var result = await _coordinator.ReviewAsync(repo, prNumber);
            results.Add(new
            {
                prNumber,
                summary = result.OverallSummary,
                violationCount = result.Violations.Count,
                agentCount = result.AgentResults.Count
            });
        }

        return Ok(new { message = "Push review completed", reviews = results });
    }

    private static string ExtractJsonBody(string body)
    {
        if (body.StartsWith("payload=", StringComparison.OrdinalIgnoreCase))
        {
            return HttpUtility.UrlDecode(body["payload=".Length..]);
        }

        // Try parsing as full form-urlencoded in case there are other fields
        var parsed = HttpUtility.ParseQueryString(body);
        var payload = parsed["payload"];
        if (!string.IsNullOrEmpty(payload))
        {
            return payload;
        }

        return body;
    }

    //Used for testing the SOP provider and review service without needing to trigger actual webhooks
    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        var repo = "iobillos-item/OpenClawAPI";
        var prNumber = 3;
        var result = await _coordinator.ReviewAsync(repo, prNumber);
        return Ok(new
        {
            message = "Test multi-agent review completed",
            summary = result.OverallSummary,
            violationCount = result.Violations.Count,
            agentCount = result.AgentResults.Count
        });
    }
}
