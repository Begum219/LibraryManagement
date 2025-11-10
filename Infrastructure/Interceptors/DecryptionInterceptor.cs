using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.Services;
using Domain.Entities;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors
{
    public class DecryptionInterceptor : IMaterializationInterceptor
    {
        private readonly IEncryptionService _encryptionService;

        public DecryptionInterceptor(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
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
            // ✅ Email şifresini çöz
            if (!string.IsNullOrEmpty(user.Email))
            {
                user.Email = _encryptionService.Decrypt(user.Email);
            }

            // ✅ FullName şifresini çöz
            if (!string.IsNullOrEmpty(user.FullName))
            {
                user.FullName = _encryptionService.Decrypt(user.FullName);
            }

            // ✅ TwoFactorSecretKey şifresini çöz
            if (!string.IsNullOrEmpty(user.TwoFactorSecretKey))
            {
                user.TwoFactorSecretKey = _encryptionService.Decrypt(user.TwoFactorSecretKey);
            }

            // ✅ RefreshToken şifresini çöz
            if (!string.IsNullOrEmpty(user.RefreshToken))
            {
                user.RefreshToken = _encryptionService.Decrypt(user.RefreshToken);
            }
        }
    }
}