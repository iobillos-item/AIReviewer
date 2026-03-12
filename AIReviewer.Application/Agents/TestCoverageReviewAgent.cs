using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class TestCoverageReviewAgent : BaseReviewAgent
{
    public override string AgentName => "Test Coverage";
    protected override string AgentType => "TestCoverage";

    public TestCoverageReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        ILogger<TestCoverageReviewAgent> logger)
        : base(llmService, promptBuilder, logger) { }
}
