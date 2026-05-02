using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Modules.Subcontractor.Interfaces;

public interface ISubcontractorService
{
    Task<SubcontractorDashboardStatsResponse> GetDashboardStatsAsync(Guid userId);
    Task<List<JobListingResponse>> ListActiveJobsAsync(string? category, string? location, string? search, int page, int pageSize);
    Task<JobListingDetailResponse> GetJobDetailAsync(Guid jobId);
    Task<OfferResponse> CreateOfferAsync(Guid userId, Guid jobId, CreateOfferRequest req);
    Task WithdrawOfferAsync(Guid userId, Guid offerId);
    Task<List<OfferResponse>> GetMyOffersAsync(Guid userId, int page, int pageSize);
    Task<List<AssignedJobResponse>> GetActiveJobsAsync(Guid userId, int page, int pageSize);
    Task<AssignedJobDetailResponse> GetActiveJobDetailAsync(Guid userId, Guid id);
    Task<AssignedJobResponse> UpdateActiveJobAsync(Guid userId, Guid id, UpdateAssignedJobRequest req);
    Task<JobReportResponse> UploadReportAsync(Guid userId, Guid assignedJobId, string fileName, string fileUrl, long? fileSize, string? fileType);
    Task<WalletResponse> GetWalletAsync(Guid userId);
}
