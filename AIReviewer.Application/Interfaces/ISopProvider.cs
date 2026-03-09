namespace AIReviewer.Application.Interfaces;

public interface ISopProvider
{
    Task<string> GetSopContentAsync();
}
