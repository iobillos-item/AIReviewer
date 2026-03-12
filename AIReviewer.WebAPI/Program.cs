using AIReviewer.Application.Agents;
using AIReviewer.Application.Interfaces;
using AIReviewer.Application.Services;
using AIReviewer.Infrastructure.GitHub;
using AIReviewer.Infrastructure.LLM;
using AIReviewer.Infrastructure.Persistence;
using AIReviewer.Infrastructure.SOP;
using AIReviewer.WebAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Application services
builder.Services.AddScoped<IPRReviewService, PRReviewService>();
builder.Services.AddScoped<ISopProvider, MarkdownSopProvider>();
builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
builder.Services.AddScoped<IReviewAggregator, ReviewAggregator>();

// Multi-agent registration — add new agents here (Open/Closed Principle)
builder.Services.AddScoped<ICodeReviewAgent, ArchitectureReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, SecurityReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, PerformanceReviewAgent>();
builder.Services.AddScoped<ICodeReviewAgent, TestCoverageReviewAgent>();

// HTTP clients for external APIs
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddHttpClient<ILLMService, OpenAiService>();

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
