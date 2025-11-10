using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Application.Interfaces.Services;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services
{
    public class AesEncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public AesEncryptionService(IConfiguration configuration)
        {
            // ✅ appsettings.json'dan şifreleme anahtarını al
            var encryptionKey = configuration["Encryption:Key"]
                ?? throw new InvalidOperationException("Encryption:Key bulunamadı! appsettings.json'a ekleyin.");

            var encryptionIv = configuration["Encryption:IV"]
                ?? throw new InvalidOperationException("Encryption:IV bulunamadı! appsettings.json'a ekleyin.");

            _key = Convert.FromBase64String(encryptionKey);
            _iv = Convert.FromBase64String(encryptionIv);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return srDecrypt.ReadToEnd();
            }
            catch (Exception)
            {
                // Eğer decrypt edilemezse (şifrelenmemiş veri), orijinali döndür
                return cipherText;
            }
        }
    }
}