using System;
using LibraryManagement.Application.DTOs.Auth;
using LibraryManagement.Application.Interfaces.Services;
using OtpNet;
using QRCoder;

namespace Infrastructure.Services
{
    public class TwoFactorService : ITwoFactorService
    {
        public TwoFactorSetupDto GenerateSetup(string email)
        {
            // Secret key üret (20 byte = 160 bit)
            var key = KeyGeneration.GenerateRandomKey(20);
            var secretKey = Base32Encoding.ToString(key);

            // Google Authenticator URI formatı
            var uri = $"otpauth://totp/LibraryManagement:{email}?secret={secretKey}&issuer=LibraryManagement";

            // QR kod oluştur
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(10);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

            return new TwoFactorSetupDto
            {
                QrCodeUrl = $"data:image/png;base64,{qrCodeBase64}",
                ManualEntryKey = secretKey
            };
        }

        public bool ValidateCode(string secretKey, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
                return false;

            try
            {
                var keyBytes = Base32Encoding.ToBytes(secretKey);
                var totp = new Totp(keyBytes);

                // RFC standart window (1 önceki ve 1 sonraki periyot)
                return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}