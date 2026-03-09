using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Infrastructure.SOP;

public class MarkdownSopProvider : ISopProvider
{
    private const string SopDirectory = "ai-reviewer/sop";
    private readonly ILogger<MarkdownSopProvider> _logger;

    public MarkdownSopProvider(ILogger<MarkdownSopProvider> logger)
    {
        _logger = logger;
    }

    public async Task<string> GetSopContentAsync()
    {
        if (!Directory.Exists(SopDirectory))
        {
            _logger.LogWarning("SOP directory not found at {Path}", SopDirectory);
            return string.Empty;
        }

        var markdownFiles = Directory.GetFiles(SopDirectory, "*.md");
        if (markdownFiles.Length == 0)
        {
            _logger.LogWarning("No SOP markdown files found in {Path}", SopDirectory);
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var file in markdownFiles.OrderBy(f => f))
        {
            var content = await File.ReadAllTextAsync(file);
            sb.AppendLine($"--- {Path.GetFileName(file)} ---");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        _logger.LogInformation("Loaded {Count} SOP files", markdownFiles.Length);
        return sb.ToString();
    }
}
