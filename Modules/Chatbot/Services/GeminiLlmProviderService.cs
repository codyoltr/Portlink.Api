using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Portlink.Api.Modules.Chatbot.Dtos;
using Portlink.Api.Modules.Chatbot.Interfaces;
using Portlink.Api.Modules.Chatbot.Settings;

namespace Portlink.Api.Modules.Chatbot.Services;

public class GeminiLlmProviderService : ILlmProviderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ChatbotSettings _settings;
    private readonly ILogger<GeminiLlmProviderService> _logger;

    public GeminiLlmProviderService(HttpClient httpClient, ChatbotSettings settings, ILogger<GeminiLlmProviderService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<LlmProviderResponse> GenerateResponseAsync(LlmPromptRequest request, CancellationToken cancellationToken)
    {
        _settings.Validate();

        var endpoint = $"{_settings.GeminiBaseUrl.TrimEnd('/')}/models/{_settings.GeminiModel}:generateContent?key={Uri.EscapeDataString(_settings.GeminiApiKey)}";

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = request.SystemPrompt }
                }
            },
            contents = request.Messages.Select(message => new
            {
                role = message.Role == "assistant" ? "model" : "user",
                parts = new[]
                {
                    new { text = message.Content }
                }
            }),
            generationConfig = new
            {
                temperature = _settings.GeminiTemperature
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini request failed. StatusCode={StatusCode} Response={Response}",
                (int)response.StatusCode,
                responseContent);

            throw new HttpRequestException("Gemini request failed.");
        }

        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseContent, JsonOptions);
        var answer = geminiResponse?.Candidates?
            .SelectMany(candidate => candidate.Content?.Parts ?? Enumerable.Empty<GeminiContentPart>())
            .Select(part => part.Text?.Trim())
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (string.IsNullOrWhiteSpace(answer))
        {
            _logger.LogWarning("Gemini returned an empty answer. RawResponse={Response}", responseContent);
            throw new HttpRequestException("Gemini returned an empty answer.");
        }

        return new LlmProviderResponse
        {
            Answer = answer,
            Provider = "gemini",
            Model = _settings.GeminiModel
        };
    }

    private sealed class GeminiGenerateContentResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        public List<GeminiContentPart>? Parts { get; set; }
    }

    private sealed class GeminiContentPart
    {
        public string? Text { get; set; }
    }
}
