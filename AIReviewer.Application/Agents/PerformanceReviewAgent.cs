using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class PerformanceReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Performance";

    public PerformanceReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        IResponseParser responseParser,
        ILogger<PerformanceReviewAgent> logger)
        : base(llmService, promptBuilder, responseParser, logger) { }
}
