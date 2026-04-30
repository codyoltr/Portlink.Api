using Portlink.Api.Modules.Messaging.Dtos;

namespace Portlink.Api.Modules.Messaging.Interfaces;

public interface IMessagingService
{
    Task<List<MessageResponse>> GetMessagesAsync(Guid userId, Guid assignedJobId);
    Task<MessageResponse> SendMessageAsync(Guid userId, Guid assignedJobId, Guid receiverId, string content);
    Task MarkReadAsync(Guid userId, Guid messageId);
}
