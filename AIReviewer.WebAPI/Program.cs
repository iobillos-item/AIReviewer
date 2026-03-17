using AIReviewer.Application.Agents;
using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Infrastructure.GitHub;
using AIReviewer.Infrastructure.LLM;
using AIReviewer.Infrastructure.Persistence;
using AIReviewer.Infrastructure.SOP;
using AIReviewer.Infrastructure.Vector;
using AIReviewer.WebAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load .env file for local development (Docker Compose handles this in production)
LoadEnvFile(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"));
LoadEnvFile(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

// Add environment variables to configuration (overrides appsettings.json)
builder.Configuration.AddEnvironmentVariables();

// Application services
builder.Services.AddScoped<IPRReviewService, PRReviewService>();
builder.Services.AddScoped<IReviewCoordinator, ReviewCoordinator>();
builder.Services.AddScoped<IDiffChunker, DiffChunker>();
builder.Services.AddScoped<IDiffRouterAgent, DiffRouterAgent>();
builder.Services.AddScoped<ISopContextRetriever, SopContextRetriever>();
builder.Services.AddScoped<IMetaReviewAgent, MetaReviewAgent>();
builder.Services.AddScoped<IAutoFixSuggestionGenerator, AutoFixSuggestionGenerator>();
builder.Services.AddScoped<IReviewAggregator, ReviewAggregator>();
builder.Services.AddScoped<IReviewCommentFormatter, ReviewCommentFormatter>();
builder.Services.AddScoped<ISopProvider, MarkdownSopProvider>();
builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
builder.Services.AddScoped<IResponseParser, ResponseParser>();
builder.Services.AddScoped<ISopIngestionService, SopIngestionService>();

// Multi-agent registration (Open/Closed: add new agents here without changing core)
builder.Services.AddScoped<ICodeReviewAgent, ArchitectureReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, SecurityReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, PerformanceReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, TestCoverageReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, DependencyReviewAgent>();

// Infrastructure
builder.Services.AddScoped<IVectorStore, VectorStoreService>();
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddHttpClient<IGitHubReviewService, GitHubReviewService>();
builder.Services.AddHttpClient<ILLMService, OpenAiService>();
builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void LoadEnvFile(string path)
{
    if (!File.Exists(path)) return;

    foreach (var line in File.ReadAllLines(path))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            continue;

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0) continue;

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim();

        // Only set if not already defined (real env vars take precedence)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}
