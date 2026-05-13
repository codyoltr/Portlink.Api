using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.DTOs.Agents;
using Portlink.Api.Modules.Auth.Dtos;

namespace Portlink.Api.Modules.Agent;

public interface IAgentService
{
    Task<AgentProfileResponse> GetProfileAsync(Guid userId);
    Task<AgentProfileResponse> UpdateProfileAsync(Guid userId, UpdateAgencyProfileRequest req);
    Task<string> UploadLogoAsync(Guid userId, string logoUrl);
    Task<AgentDashboardStatsResponse> GetDashboardStatsAsync(Guid userId);
    Task<List<JobListingResponse>> GetMyJobsAsync(Guid userId, string? status, string? category, int page, int pageSize);
    Task<List<JobListingResponse>> ListMarketplaceJobsAsync(string? category, string? location, string? search, int page, int pageSize);
    Task<JobListingDetailResponse> GetJobDetailAsync(Guid userId, Guid jobId);
    Task<JobListingResponse> CreateJobAsync(Guid userId, CreateJobListingRequest req);
    Task<JobListingResponse> UpdateJobAsync(Guid userId, Guid jobId, UpdateJobListingRequest req);
    Task DeleteJobAsync(Guid userId, Guid jobId);
    
    Task<List<OfferResponse>> GetJobOffersAsync(Guid userId, Guid jobId);
    Task<AgentOffersDashboardResponse> GetAllOffersAsync(Guid userId, AgentOffersQueryRequest request);
    Task<AgentOfferDetailResponse> GetOfferDetailAsync(Guid userId, Guid offerId);
    Task<AssignedJobResponse> AcceptOfferAsync(Guid userId, Guid offerId);
    Task RejectOfferAsync(Guid userId, Guid offerId);
    
    Task<List<AssignedJobResponse>> GetAssignedJobsAsync(Guid userId, string? status, int page, int pageSize);
    Task<AssignedJobDetailResponse> GetAssignedJobDetailAsync(Guid userId, Guid id);
    Task<JobLogResponse> AddJobLogAsync(Guid userId, Guid assignedJobId, AddJobLogRequest req);
    Task RequestReportAsync(Guid userId, Guid assignedJobId);
    Task<AssignedJobResponse> CompleteJobAsync(Guid userId, Guid assignedJobId);
    Task<JobFileResponse> UploadJobFileAsync(Guid userId, Guid jobId, string fileName, string fileUrl, long? fileSize, string? fileType);

    Task<List<Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse>> GetSubcontractorsAsync(string? search);
    Task<Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse> GetSubcontractorByIdAsync(Guid userId, Guid subcontractorId);
    Task RateSubcontractorAsync(Guid userId, Guid subcontractorId, decimal rating);
}
