using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class ArchitectureReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Architecture";
    protected override string AgentType => "Architecture";

    public ArchitectureReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        ILogger<ArchitectureReviewAgent> logger)
        : base(llmService, promptBuilder, logger) { }
}
