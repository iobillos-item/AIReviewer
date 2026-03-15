using System.Diagnostics;
using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public abstract class BaseReviewAgent : ICodeReviewAgent
{
    private readonly ILLMService _llmService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IResponseParser _responseParser;
    private readonly ILogger _logger;

    public abstract string AgentName { get; }

    protected BaseReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        IResponseParser responseParser,
        ILogger logger)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _responseParser = responseParser;
        _logger = logger;
    }

    public async Task<AgentReviewResult> ReviewAsync(DiffChunk chunk, string sopContext)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var prompt = _promptBuilder.BuildAgentPrompt(AgentName, chunk, sopContext);
            var response = await _llmService.GetCompletionAsync(prompt);
            sw.Stop();

            var result = _responseParser.ParseAgentResponse(response, AgentName);
            result.Duration = sw.Elapsed;

            _logger.LogInformation(
                "Agent {Agent} completed in {Duration}ms with {Count} violations",
                AgentName, sw.ElapsedMilliseconds, result.Violations.Count);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Agent {Agent} failed", AgentName);

            return new AgentReviewResult
            {
                AgentName = AgentName,
                HasError = true,
                ErrorMessage = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }
}
