using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IMetaReviewAgent
{
    Task<IEnumerable<AgentViolation>> ValidateAsync(IEnumerable<AgentViolation> violations);
}
