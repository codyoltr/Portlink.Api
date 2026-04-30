using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;

namespace Portlink.Api.Modules.Agent;

public interface IAgentService
{
    Task<AgentDashboardStatsResponse> GetDashboardStatsAsync(Guid userId);
    Task<List<JobListingResponse>> GetMyJobsAsync(Guid userId, string? status, string? category, int page, int pageSize);
    Task<JobListingDetailResponse> GetJobDetailAsync(Guid userId, Guid jobId);
    Task<JobListingResponse> CreateJobAsync(Guid userId, CreateJobListingRequest req);
    Task<JobListingResponse> UpdateJobAsync(Guid userId, Guid jobId, UpdateJobListingRequest req);
    Task DeleteJobAsync(Guid userId, Guid jobId);
    
    Task<List<OfferResponse>> GetJobOffersAsync(Guid userId, Guid jobId);
    Task<AssignedJobResponse> AcceptOfferAsync(Guid userId, Guid offerId);
    Task RejectOfferAsync(Guid userId, Guid offerId);
    
    Task<List<AssignedJobResponse>> GetAssignedJobsAsync(Guid userId, string? status, int page, int pageSize);
    Task<AssignedJobDetailResponse> GetAssignedJobDetailAsync(Guid userId, Guid id);
    Task<JobLogResponse> AddJobLogAsync(Guid userId, Guid assignedJobId, AddJobLogRequest req);
    Task RequestReportAsync(Guid userId, Guid assignedJobId);
    Task<AssignedJobResponse> CompleteJobAsync(Guid userId, Guid assignedJobId);
    Task<JobFileResponse> UploadJobFileAsync(Guid userId, Guid jobId, string fileName, string fileUrl, long? fileSize, string? fileType);
}
