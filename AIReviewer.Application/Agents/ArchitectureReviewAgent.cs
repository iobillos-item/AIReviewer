using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class ArchitectureReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Architecture";

    public ArchitectureReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        IResponseParser responseParser,
        ILogger<ArchitectureReviewAgent> logger)
        : base(llmService, promptBuilder, responseParser, logger) { }
}
