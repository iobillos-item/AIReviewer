namespace AIReviewer.Application.Interfaces;

public interface ISopContextRetriever
{
    Task<string> GetRelevantContextAsync(string diffChunk);
}
