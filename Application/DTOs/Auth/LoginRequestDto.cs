namespace LibraryManagement.Application.DTOs.Auth
{
    public class LoginRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? TwoFactorCode { get; set; } // 2FA kodu (opsiyonel)
    }
}