using System;
using Application.Interfaces.Services;
using Domain.Entities;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Interceptors
{
    public class DecryptionInterceptor : IMaterializationInterceptor
    {
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<DecryptionInterceptor> _logger;

        public DecryptionInterceptor(
            IEncryptionService encryptionService,
            ILogger<DecryptionInterceptor> logger)
        {
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
        {
            if (entity is User user)
            {
                DecryptUser(user);
            }

            return entity;
        }

        public InterceptionResult<object> CreatingInstance(
            MaterializationInterceptionData materializationData,
            InterceptionResult<object> result)
        {
            return result;
        }

        private void DecryptUser(User user)
        {
            user.Email = SafeDecrypt(user.Email, user.Id, "Email");
            user.FullName = SafeDecrypt(user.FullName, user.Id, "FullName");
            user.TwoFactorSecretKey = SafeDecrypt(user.TwoFactorSecretKey, user.Id, "TwoFactorSecretKey");
            user.RefreshToken = SafeDecrypt(user.RefreshToken, user.Id, "RefreshToken");
        }

        private string SafeDecrypt(string value, int userId, string fieldName)
        {
            // Email VE FullName'i çöz
            if ((fieldName == "Email" || fieldName == "FullName") && !string.IsNullOrEmpty(value))
            {
                try
                {
                    return _encryptionService.Decrypt(value);
                }
                catch
                {
                    return value; // Çözülemezse olduğu gibi döndür
                }
            }

            
            return value;
        }
    }
}