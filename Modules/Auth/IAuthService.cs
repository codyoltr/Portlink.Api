using Portlink.Api.DTOs.Auth;

namespace Portlink.Api.Modules.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAgentAsync(RegisterAgentRequest req);
    Task<AuthResponse> RegisterSubcontractorAsync(RegisterSubcontractorRequest req);
    Task<AuthResponse> LoginAsync(LoginRequest req);
    Task<AuthResponse> RefreshAsync(string rawRefreshToken);
    Task LogoutAsync(Guid userId);
    Task<UserProfileResponse> GetMeAsync(Guid userId);
}
