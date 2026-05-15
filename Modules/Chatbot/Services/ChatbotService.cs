using System.Net.Http;
using Microsoft.Extensions.Logging;
using Portlink.Api.Modules.Chatbot.Dtos;
using Portlink.Api.Modules.Chatbot.Interfaces;
using Portlink.Api.Modules.Chatbot.Prompts;
using Portlink.Api.Modules.Chatbot.Settings;

namespace Portlink.Api.Modules.Chatbot.Services;

public class ChatbotService : IChatbotService
{
    private readonly ILlmProviderService _llmProviderService;
    private readonly ChatbotSettings _settings;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(ILlmProviderService llmProviderService, ChatbotSettings settings, ILogger<ChatbotService> logger)
    {
        _llmProviderService = llmProviderService;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ChatbotMessageResponse> SendMessageAsync(ChatbotUserContext currentUser, ChatbotMessageRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var prompt = ChatbotPromptBuilder.Build(currentUser, request);
            var providerResponse = await _llmProviderService.GenerateResponseAsync(prompt, cancellationToken);

            return new ChatbotMessageResponse
            {
                Answer = providerResponse.Answer,
                Provider = providerResponse.Provider,
                Model = providerResponse.Model
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Chatbot provider timed out. UserId={UserId} Role={Role}",
                currentUser.UserId,
                currentUser.Role);

            throw new HttpRequestException("The assistant is currently unavailable. Please try again later.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Chatbot provider request failed. UserId={UserId} Role={Role}",
                currentUser.UserId,
                currentUser.Role);

            throw new HttpRequestException("The assistant is currently unavailable. Please try again later.");
        }
    }

    private void ValidateRequest(ChatbotMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new InvalidOperationException("Message is required.");
        }

        request.Message = request.Message.Trim();
        if (request.Message.Length > _settings.MaxMessageLength)
        {
            throw new InvalidOperationException($"Message cannot exceed {_settings.MaxMessageLength} characters.");
        }

        if (request.RecentMessages is { Count: > 0 })
        {
            if (request.RecentMessages.Count > _settings.MaxRecentMessages)
            {
                throw new InvalidOperationException($"Recent message count cannot exceed {_settings.MaxRecentMessages}.");
            }

            foreach (var message in request.RecentMessages)
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    throw new InvalidOperationException("Recent messages cannot be empty.");
                }

                message.Content = message.Content.Trim();
                if (message.Content.Length > _settings.MaxRecentMessageLength)
                {
                    throw new InvalidOperationException($"Recent message length cannot exceed {_settings.MaxRecentMessageLength} characters.");
                }

                message.Role = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role.Trim().ToLowerInvariant();
            }
        }

        if (request.Context is not null)
        {
            request.Context.Route = TrimOrNull(request.Context.Route, _settings.MaxContextLength, "Context route");
            request.Context.PageName = TrimOrNull(request.Context.PageName, _settings.MaxContextLength, "Context pageName");
            request.Context.UserRole = TrimOrNull(request.Context.UserRole, _settings.MaxContextLength, "Context userRole");
        }
    }

    private static string? TrimOrNull(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
