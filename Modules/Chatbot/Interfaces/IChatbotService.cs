using Portlink.Api.Modules.Chatbot.Dtos;

namespace Portlink.Api.Modules.Chatbot.Interfaces;

public interface IChatbotService
{
    Task<ChatbotMessageResponse> SendMessageAsync(ChatbotUserContext currentUser, ChatbotMessageRequest request, CancellationToken cancellationToken);
}
