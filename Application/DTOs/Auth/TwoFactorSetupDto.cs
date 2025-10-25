namespace LibraryManagement.Application.DTOs.Auth
{
    public class TwoFactorSetupDto
    {
        public string QrCodeUrl { get; set; } = null!;
        public string ManualEntryKey { get; set; } = null!;
    }
}