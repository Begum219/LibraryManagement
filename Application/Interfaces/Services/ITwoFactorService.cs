using LibraryManagement.Application.DTOs.Auth;

namespace LibraryManagement.Application.Interfaces.Services
{
    public interface ITwoFactorService
    {
        TwoFactorSetupDto GenerateSetup(string email);
        bool ValidateCode(string secretKey, string code);
    }
}