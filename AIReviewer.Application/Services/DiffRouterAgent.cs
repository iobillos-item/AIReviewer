using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Services;

public class DiffRouterAgent : IDiffRouterAgent
{
    private readonly ILLMService _llmService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IEnumerable<ICodeReviewAgent> _agents;
    private readonly ILogger<DiffRouterAgent> _logger;

    public DiffRouterAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        IEnumerable<ICodeReviewAgent> agents,
        ILogger<DiffRouterAgent> logger)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _agents = agents;
        _logger = logger;
    }

    public async Task<IEnumerable<RoutedChunk>> RouteAsync(IEnumerable<DiffChunk> chunks)
    {
        var availableAgents = _agents.Select(a => a.AgentName).ToList();
        var routedChunks = new List<RoutedChunk>();

        foreach (var chunk in chunks)
        {
            try
            {
                var prompt = _promptBuilder.BuildRouterPrompt(chunk, availableAgents);
                var response = await _llmService.GetCompletionAsync(prompt);
                var assigned = ParseRouterResponse(response, availableAgents);

                if (assigned.Count == 0)
                    assigned = availableAgents; // fallback: send to all

                _logger.LogInformation("Routed {File} to [{Agents}]",
                    chunk.FileName, string.Join(", ", assigned));

                routedChunks.Add(new RoutedChunk { Chunk = chunk, AssignedAgents = assigned });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Router failed for {File}, assigning all agents", chunk.FileName);
                routedChunks.Add(new RoutedChunk { Chunk = chunk, AssignedAgents = availableAgents });
            }
        }

        return routedChunks;
    }

    private static List<string> ParseRouterResponse(string response, List<string> available)
    {
        return available
            .Where(agent => response.Contains(agent, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
