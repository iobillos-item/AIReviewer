using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Application.Agents;

public class TestCoverageReviewAgent : BaseReviewAgent
{
    public override string AgentName => "TestCoverage";

    public TestCoverageReviewAgent(
        ILLMService llmService,
        IPromptBuilder promptBuilder,
        IResponseParser responseParser,
        ILogger<TestCoverageReviewAgent> logger)
        : base(llmService, promptBuilder, responseParser, logger) { }
}
