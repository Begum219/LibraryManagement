using LibraryManagement.Application.DTOs.Auth;

namespace LibraryManagement.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<TokenResponseDto> LoginAsync(LoginRequestDto request);
        Task<TokenResponseDto> RegisterAsync(RegisterRequestDto request);
        Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<TwoFactorSetupDto> EnableTwoFactorAsync(int userId);
        Task<bool> VerifyTwoFactorCodeAsync(int userId, string code);
        Task DisableTwoFactorAsync(int userId);
    }
}