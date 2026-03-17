using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IAutoFixSuggestionGenerator
{
    Task<AutoFixResult> GenerateFixAsync(AgentViolation violation, DiffChunk chunk);
}
