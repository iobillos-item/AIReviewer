using System.Text.Json.Serialization;

namespace AIReviewer.Application.DTOs;

/// <summary>
/// Payload for pull_request webhook events.
/// </summary>
public class PullRequestWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("repository")]
    public RepositoryInfo? Repository { get; set; }

    [JsonPropertyName("pull_request")]
    public PullRequestInfo? PullRequest { get; set; }
}

/// <summary>
/// Payload for push webhook events.
/// </summary>
public class PushWebhookPayload
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("before")]
    public string Before { get; set; } = string.Empty;

    [JsonPropertyName("after")]
    public string After { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public RepositoryInfo? Repository { get; set; }

    [JsonPropertyName("commits")]
    public List<CommitInfo> Commits { get; set; } = new();

    [JsonPropertyName("head_commit")]
    public CommitInfo? HeadCommit { get; set; }
}

public class CommitInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class RepositoryInfo
{
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;
}

public class PullRequestInfo
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}
