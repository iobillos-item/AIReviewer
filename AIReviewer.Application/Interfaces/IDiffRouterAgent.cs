using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IDiffRouterAgent
{
    Task<IEnumerable<RoutedChunk>> RouteAsync(IEnumerable<DiffChunk> chunks);
}
