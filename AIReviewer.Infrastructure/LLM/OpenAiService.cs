using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIReviewer.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Infrastructure.LLM;

public class OpenAiService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = configuration["OpenAI:ApiKey"];
        _model = configuration["OpenAI:Model"] ?? "gpt-4o";

        _httpClient.BaseAddress = new Uri("https://api.openai.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(requestBody);

        const int maxRetries = 3;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("v1/chat/completions", content);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError("OpenAI rate limit exceeded after {MaxRetries} retries", maxRetries);
                    response.EnsureSuccessStatusCode(); // throws
                }

                // Use Retry-After header if available, otherwise exponential backoff
                var delay = response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));

                _logger.LogWarning("Rate limited by OpenAI. Retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                    delay.TotalSeconds, attempt + 1, maxRetries);

                await Task.Delay(2000);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var result = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            _logger.LogInformation("LLM completion received ({Length} chars)", result.Length);
            return result;
        }

        throw new HttpRequestException("Unexpected retry loop exit");
    }
}
