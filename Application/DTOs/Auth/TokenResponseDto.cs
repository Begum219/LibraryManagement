namespace LibraryManagement.Application.DTOs.Auth
{
    public class TokenResponseDto
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public string? TwoFactorQrCode { get; set; } // İlk kurulum için QR kod
        public bool RequiresTwoFactor { get; set; } // 2FA gerekiyor mu?
    }
}