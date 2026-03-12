using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class SecurityReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Security";
    protected override string AgentType => "Security";

    public SecurityReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        ILogger<SecurityReviewAgent> logger)
        : base(llmService, promptBuilder, logger) { }
}
