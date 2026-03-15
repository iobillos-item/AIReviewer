using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class DependencyReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Dependency";

    public DependencyReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        IResponseParser responseParser,
        ILogger<DependencyReviewAgent> logger)
        : base(llmService, promptBuilder, responseParser, logger) { }
}
