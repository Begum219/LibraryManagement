using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryManagement.Application.DTOs.Auth;
using LibraryManagement.Application.Interfaces;
using LibraryManagement.Application.Interfaces.Services;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using LibraryManagement.Application.Interfaces.UnitOfWork;
using Application.Interfaces.Services;

namespace Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly ITwoFactorService _twoFactorService;
        private readonly IConfiguration _configuration;
        private readonly IEncryptionService _encryptionService;  // ✅ EKLE
        public AuthService(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            ITwoFactorService twoFactorService,
            IConfiguration configuration,
            IEncryptionService encryptionService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _twoFactorService = twoFactorService;
            _configuration = configuration;
            _encryptionService = encryptionService;  // ✅ EKLE
        }

        public async Task<TokenResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            if (await _unitOfWork.Users.UserExistsAsync(request.Email))
                throw new Exception("Bu email zaten kayıtlı!");

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                FullName = request.FullName,
                Role = request.Role,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                TwoFactorEnabled = false,
                PublicId = Guid.NewGuid(),      // ✅ EKLE
                IsDeleted = false               // ✅ EKLE
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!));

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(
                    int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"]!)),
                RequiresTwoFactor = false
            };
        }

        public async Task<TokenResponseDto> LoginAsync(LoginRequestDto request)
        {
            try
            {
                // ✅ Email'i şifrele
                var encryptedEmail = _encryptionService.Encrypt(request.Email);

                var user = await _unitOfWork.Users.GetByEmailAsync(encryptedEmail);
                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                    throw new Exception("Email veya şifre hatalı!");

                if (user.IsActive != true)
                    throw new Exception("Hesabınız aktif değil!");

                // 2FA gerekiyor mu kontrol eder
                bool requires2FA = user.TwoFactorEnabled && string.IsNullOrEmpty(request.TwoFactorCode);
                if (requires2FA)
                {
                    return new TokenResponseDto
                    {
                        AccessToken = null,
                        RefreshToken = null,
                        ExpiresAt = DateTime.UtcNow,
                        RequiresTwoFactor = true
                    };
                }

                // Eğer kod girilmişse ve doğrulanmışsa
                if (user.TwoFactorEnabled && !string.IsNullOrEmpty(request.TwoFactorCode))
                {
                    if (!_twoFactorService.ValidateCode(user.TwoFactorSecretKey!, request.TwoFactorCode))
                        throw new Exception("2FA kodu hatalı!");
                }

                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                    int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!));
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                return new TokenResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(
                        int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"]!)),
                    RequiresTwoFactor = false
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Login hatası: {ex.Message}", ex);
            }
        }

        public async Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.RefreshToken == request.RefreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new Exception("Geçersiz veya süresi dolmuş refresh token!");

            var accessToken = _tokenService.GenerateAccessToken(user);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!));
            user.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(
                    int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"]!)),
                RequiresTwoFactor = false
            };
        }

        public async Task<TwoFactorSetupDto> EnableTwoFactorAsync(int userId)
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
                throw new Exception("Kullanıcı bulunamadı!");

            var setup = _twoFactorService.GenerateSetup(user.Email);

            // Sadece secret key'i kaydet, henüz aktif etme
            user.TwoFactorSecretKey = setup.ManualEntryKey;
            user.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            return setup;
        }

        public async Task<bool> VerifyTwoFactorCodeAsync(int userId, string code)
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);

            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecretKey))
                throw new Exception("2FA ayarları bulunamadı!");

            var isValid = _twoFactorService.ValidateCode(user.TwoFactorSecretKey, code);

            if (isValid)
            {
                user.TwoFactorEnabled = true;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();
            }

            return isValid;
        }

        public async Task DisableTwoFactorAsync(int userId)
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            var user = users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
                throw new Exception("Kullanıcı bulunamadı!");

            user.TwoFactorEnabled = false;
            user.TwoFactorSecretKey = null;
            user.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}