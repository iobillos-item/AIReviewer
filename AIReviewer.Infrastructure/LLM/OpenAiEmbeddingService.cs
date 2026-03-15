using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AIReviewer.Infrastructure.LLM;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;

    public OpenAiEmbeddingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var apiKey = configuration["OpenAI:ApiKey"];

        _httpClient.BaseAddress = new Uri("https://api.openai.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var requestBody = new { model = "text-embedding-3-small", input = text };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("v1/embeddings", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var embeddingArray = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        return embeddingArray.EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();
    }
}
