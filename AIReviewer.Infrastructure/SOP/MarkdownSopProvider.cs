using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Infrastructure.SOP;

public class MarkdownSopProvider : ISopProvider
{
    private readonly string _sopDirectory;
    private readonly ILogger<MarkdownSopProvider> _logger;

    public MarkdownSopProvider(ILogger<MarkdownSopProvider> logger)
    {
        _logger = logger;
        _sopDirectory = Path.Combine(AppContext.BaseDirectory, "ai-reviewer", "sop");
    }

    public async Task<string> GetSopContentAsync()
    {
        if (!Directory.Exists(_sopDirectory))
        {
            _logger.LogWarning("SOP directory not found at {Path}", _sopDirectory);
            return string.Empty;
        }

        var markdownFiles = Directory.GetFiles(_sopDirectory, "*.md");
        if (markdownFiles.Length == 0)
        {
            _logger.LogWarning("No SOP markdown files found in {Path}", _sopDirectory);
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var file in markdownFiles.OrderBy(f => f))
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                sb.AppendLine($"--- {Path.GetFileName(file)} ---");
                sb.AppendLine(content);
                sb.AppendLine();


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SOP file {File}", file);
                continue;
            }
        }

        _logger.LogInformation("Loaded {Count} SOP files", markdownFiles.Length);
        return sb.ToString();
    }
}
