using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Interfaces;

public interface IDiffChunker
{
    Task<IEnumerable<DiffChunk>> ChunkAsync(string diff);
}
