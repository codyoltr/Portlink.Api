using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Messaging.Dtos;

namespace Portlink.Api.Modules.Messaging.Interfaces;

public interface IMessagingService
{
    Task<ConversationResponse> StartConversationAsync(Guid userId, StartConversationRequest request);
    Task<List<ConversationResponse>> GetConversationsAsync(Guid userId);
    Task<PaginatedResponse<ConversationMessageResponse>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize);
    Task<ConversationMessageResponse> SendMessageAsync(Guid userId, Guid conversationId, SendConversationMessageRequest request);
    Task<ConversationMessageResponse> EditMessageAsync(Guid userId, Guid conversationId, Guid messageId, EditConversationMessageRequest request);
    Task DeleteMessageForMeAsync(Guid userId, Guid conversationId, Guid messageId);
    Task DeleteMessageForEveryoneAsync(Guid userId, Guid conversationId, Guid messageId);
    Task ClearConversationHistoryAsync(Guid userId, Guid conversationId);
    Task MarkConversationReadAsync(Guid userId, Guid conversationId);
}
