namespace AIReviewer.Application.Interfaces;

public interface ILLMService
{
    Task<string> GetCompletionAsync(string prompt);
}
