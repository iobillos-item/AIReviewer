using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IReviewCommentFormatter
{
    string Format(UnifiedReviewResult result);
}
