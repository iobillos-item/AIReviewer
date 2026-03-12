using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class PerformanceReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Performance";
    protected override string AgentType => "Performance";

    public PerformanceReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        ILogger<PerformanceReviewAgent> logger)
        : base(llmService, promptBuilder, logger) { }
}
