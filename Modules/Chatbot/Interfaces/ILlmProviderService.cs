using Portlink.Api.Modules.Chatbot.Dtos;

namespace Portlink.Api.Modules.Chatbot.Interfaces;

public interface ILlmProviderService
{
    Task<LlmProviderResponse> GenerateResponseAsync(LlmPromptRequest request, CancellationToken cancellationToken);
}
