using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.Services;
using Domain.Entities;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors
{
    public class EncryptionInterceptor : SaveChangesInterceptor
    {
        private readonly IEncryptionService _encryptionService;

        public EncryptionInterceptor(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            EncryptData(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            EncryptData(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void EncryptData(DbContext? context)
        {
            if (context == null) return;

            var entries = context.ChangeTracker.Entries<User>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var user = entry.Entity;

                // ✅ Email şifrele (eğer henüz şifrelenmemişse)
                if (!string.IsNullOrEmpty(user.Email) && !IsEncrypted(user.Email))
                {
                    user.Email = _encryptionService.Encrypt(user.Email);
                }

                // ✅ FullName şifrele (eğer henüz şifrelenmemişse)
                if (!string.IsNullOrEmpty(user.FullName) && !IsEncrypted(user.FullName))
                {
                    user.FullName = _encryptionService.Encrypt(user.FullName);
                }

                // ✅ TwoFactorSecretKey şifrele (eğer henüz şifrelenmemişse)
                if (!string.IsNullOrEmpty(user.TwoFactorSecretKey) && !IsEncrypted(user.TwoFactorSecretKey))
                {
                    user.TwoFactorSecretKey = _encryptionService.Encrypt(user.TwoFactorSecretKey);
                }

                // ✅ RefreshToken şifrele (eğer henüz şifrelenmemişse)
                if (!string.IsNullOrEmpty(user.RefreshToken) && !IsEncrypted(user.RefreshToken))
                {
                    user.RefreshToken = _encryptionService.Encrypt(user.RefreshToken);
                }
            }
        }

        private bool IsEncrypted(string value)
        {
            // Base64 formatında mı kontrol et (şifreli veriler Base64)
            if (string.IsNullOrEmpty(value) || value.Length < 20)
                return false;

            try
            {
                // Base64 decode edilebiliyor mu?
                var bytes = System.Convert.FromBase64String(value);
                return bytes.Length > 0;
            }
            catch
            {
                // Base64 değilse şifrelenmemiş demektir
                return false;
            }
        }
    }
}