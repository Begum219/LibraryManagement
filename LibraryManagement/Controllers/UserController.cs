using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using System.Security.Claims;
using BCrypt.Net;
using LibraryManagement.Application.Interfaces.Services;

namespace LibraryManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserController> _logger;
        private readonly ICacheService _cacheService;

        public UserController(IUnitOfWork unitOfWork, ILogger<UserController> logger, ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Tüm kullanıcıları listele (Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _unitOfWork.Users.GetAllAsync();

                // Şifreleri gizle, PublicId göster
                var usersWithoutPasswords = users.Select(u => new
                {
                    u.PublicId,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.CreatedDate,
                    u.UpdatedDate,
                    u.IsActive,
                    u.TwoFactorEnabled
                });

                return Ok(new { success = true, data = usersWithoutPasswords });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcılar listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı profili getir (PublicId ile + IDOR korumalı)
        /// </summary>
        [Authorize]
        [HttpGet("{publicId:guid}")]  // ← :guid constraint ekledik
        public async Task<IActionResult> GetUserByPublicId(Guid publicId)
        {
            try
            {
                // 1. PublicId'den kullanıcıyı bul
                var user = await _unitOfWork.Users.GetByPublicIdAsync(publicId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                // 2. Token'dan mevcut kullanıcıyı al
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // 3. ✅ IDOR KONTROLÜ: Sadece kendi profili veya Admin
                if (user.Id != currentUserId && currentUserRole != "Admin")
                {
                    _logger.LogWarning("IDOR denemesi: Kullanıcı {CurrentUserId} başkasının profiline erişmeye çalıştı: {TargetUserId}",
                        currentUserId, user.Id);
                    return Forbid();
                }

                // 4. Cache kontrolü
                var cacheKey = $"user:{user.Id}:profile";
                var cachedUser = await _cacheService.GetAsync<User>(cacheKey);

                if (cachedUser != null)
                {
                    _logger.LogInformation("Kullanıcı profili cache'ten geldi: {UserId}", user.Id);
                    return Ok(new { success = true, data = cachedUser, source = "cache" });
                }

                // 5. Cache'e kaydet
                await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromMinutes(5));

                _logger.LogInformation("Kullanıcı profili getirildi: {UserId}", user.Id);
                return Ok(new { success = true, data = user, source = "database" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı getirme hatası: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kendi profilini getir (cache'li)
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var cacheKey = $"user:profile:{userId}";

                var cached = await _cacheService.GetAsync<User>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("Kullanıcı profili cache'ten geldi: {UserId}", userId);
                    return Ok(new { success = true, data = cached, source = "cache" });
                }

                var user = await _unitOfWork.Users.GetByIdAsync(userId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromMinutes(30));

                _logger.LogInformation("Kullanıcı profili veritabanından geldi: {UserId}", userId);
                return Ok(new { success = true, data = user, source = "database" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil getirme hatası");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kendi profil bilgilerini getir (özet)
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                var userProfile = new
                {
                    user.PublicId,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.CreatedDate,
                    user.TwoFactorEnabled
                };

                return Ok(new { success = true, data = userProfile });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil bilgileri getirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Profil güncelle
        /// </summary>
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                user.FullName = dto.FullName;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                await _cacheService.RemoveAsync($"user:profile:{userId}");
                await _cacheService.RemoveAsync($"user:{userId}:profile");

                _logger.LogInformation("Profil güncellendi: UserId={UserId}", userId);
                return Ok(new { success = true, message = "Profil başarıyla güncellendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil güncellenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Şifre değiştir
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
                    return BadRequest(new { success = false, message = "Eski şifre hatalı" });

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Şifre değiştirildi: UserId={UserId}", userId);
                return Ok(new { success = true, message = "Şifre başarıyla değiştirildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre değiştirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı rolü değiştir (Admin - PublicId ile)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{publicId:guid}/change-role")]  // ← PublicId + constraint
        public async Task<IActionResult> ChangeUserRole(Guid publicId, [FromBody] ChangeRoleDto dto)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByPublicIdAsync(publicId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                var validRoles = new[] { "Admin", "Librarian", "Member" };
                if (!validRoles.Contains(dto.NewRole))
                    return BadRequest(new { success = false, message = "Geçersiz rol" });

                user.Role = dto.NewRole;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kullanıcı rolü değiştirildi: UserId={UserId}, NewRole={NewRole}", user.Id, dto.NewRole);
                return Ok(new { success = true, message = "Kullanıcı rolü başarıyla değiştirildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı rolü değiştirilirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcıyı aktif/pasif yap (Admin - PublicId ile)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{publicId:guid}/toggle-status")]  // ← PublicId + constraint
        public async Task<IActionResult> ToggleUserStatus(Guid publicId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByPublicIdAsync(publicId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                user.IsActive = !user.IsActive;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                var status = user.IsActive == true ? "aktif" : "pasif";
                _logger.LogInformation("Kullanıcı durumu değiştirildi: UserId={UserId}, Status={Status}", user.Id, status);
                return Ok(new { success = true, message = $"Kullanıcı {status} hale getirildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı durumu değiştirilirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı sil (soft delete - Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{publicId:guid}")]
        public async Task<IActionResult> DeleteUser(Guid publicId)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var user = await _unitOfWork.Users.GetByPublicIdAsync(publicId);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                if (currentUserId == user.Id)
                    return BadRequest(new { success = false, message = "Kendi hesabınızı silemezsiniz" });

                // ✅ Soft Delete (kim sildi bilgisi ile)
                await _unitOfWork.Users.SoftDeleteAsync(user, currentUserId);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kullanıcı soft delete edildi: UserId={UserId}, DeletedBy={DeletedBy}",
                    user.Id, currentUserId);

                return Ok(new { success = true, message = "Kullanıcı başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı silinirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı arama (Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new { success = false, message = "Arama kelimesi gerekli" });

                var users = await _unitOfWork.Users.FindAsync(u =>
                    u.Email.Contains(query) ||
                    u.FullName.Contains(query)
                );

                var usersWithoutPasswords = users.Select(u => new
                {
                    u.PublicId,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.IsActive
                });

                return Ok(new { success = true, data = usersWithoutPasswords });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı arama yapılırken hata oluştu: {Query}", query);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        /// <summary>
        /// Maskelenmiş kullanıcı listesi (Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("masked")]
        public async Task<IActionResult> GetMaskedUsers()
        {
            try
            {
                var users = await _unitOfWork.Users.GetAllAsync();

                var maskedUsers = users.Select(u => new
                {
                    u.PublicId,
                    Email = MaskEmail(u.Email),
                    FullName = MaskName(u.FullName),
                    u.Role,
                    u.IsActive
                });

                return Ok(new { success = true, data = maskedUsers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Maskelenmiş kullanıcı listesi alınırken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// E-mail maskesi (örn: johndoe@gmail.com → j*****e@gmail.com)
        /// </summary>
        private string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts[0].Length <= 2)
                return "***@" + parts[1];

            return parts[0][0] + new string('*', parts[0].Length - 2) + parts[0][^1] + "@" + parts[1];
        }

        /// <summary>
        /// İsim maskesi (örn: Ahmet Yılmaz → A**** Y****)
        /// </summary>
        private string MaskName(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return string.Join(" ", parts.Select(p =>
                p.Length <= 1
                    ? "*"
                    : p[0] + new string('*', p.Length - 1)
            ));
        }


    }

    // DTOs
    public class UpdateProfileDto
    {
        public string FullName { get; set; } = null!;
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    public class ChangeRoleDto
    {
        public string NewRole { get; set; } = null!;
    }
}