using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using System.Security.Claims;
using BCrypt.Net;

namespace LibraryManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserController> _logger;

        public UserController(IUnitOfWork unitOfWork, ILogger<UserController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
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

                // Şifreleri gizle
                var usersWithoutPasswords = users.Select(u => new
                {
                    u.Id,
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
        /// ID'ye göre kullanıcı getir (Admin veya kendi profili)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Admin değilse sadece kendi profilini görebilir
                if (userRole != "Admin" && currentUserId != id)
                    return Forbid();

                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == id);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                // Şifreyi gizle
                var userWithoutPassword = new
                {
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.CreatedDate,
                    user.UpdatedDate,
                    user.IsActive,
                    user.TwoFactorEnabled
                };

                return Ok(new { success = true, data = userWithoutPassword });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı getirilirken hata oluştu: {UserId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kendi profil bilgilerini getir
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
                    user.Id,
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

                // Güncelleme
                user.FullName = dto.FullName;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

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

                // Eski şifre kontrolü
                if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
                    return BadRequest(new { success = false, message = "Eski şifre hatalı" });

                // Yeni şifre hashleme
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
        /// Kullanıcı rolü değiştir (Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/change-role")]
        public async Task<IActionResult> ChangeUserRole(int id, [FromBody] ChangeRoleDto dto)
        {
            try
            {
                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == id);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                // Rol kontrolü
                var validRoles = new[] { "Admin", "Librarian", "Member" };
                if (!validRoles.Contains(dto.NewRole))
                    return BadRequest(new { success = false, message = "Geçersiz rol" });

                user.Role = dto.NewRole;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kullanıcı rolü değiştirildi: UserId={UserId}, NewRole={NewRole}", id, dto.NewRole);
                return Ok(new { success = true, message = "Kullanıcı rolü başarıyla değiştirildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı rolü değiştirilirken hata oluştu: {UserId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcıyı aktif/pasif yap (Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == id);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                user.IsActive = !user.IsActive;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                var status = user.IsActive == true ? "aktif" : "pasif";
                _logger.LogInformation("Kullanıcı durumu değiştirildi: UserId={UserId}, Status={Status}", id, status);
                return Ok(new { success = true, message = $"Kullanıcı {status} hale getirildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı durumu değiştirilirken hata oluştu: {UserId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı sil (soft delete - Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                // Kendi hesabını silemesin
                if (currentUserId == id)
                    return BadRequest(new { success = false, message = "Kendi hesabınızı silemezsiniz" });

                var users = await _unitOfWork.Users.GetAllAsync();
                var user = users.FirstOrDefault(u => u.Id == id);

                if (user == null)
                    return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

                // Soft delete
                user.IsActive = false;
                user.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kullanıcı silindi (soft delete): UserId={UserId}", id);
                return Ok(new { success = true, message = "Kullanıcı başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı silinirken hata oluştu: {UserId}", id);
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

                // Şifreleri gizle
                var usersWithoutPasswords = users.Select(u => new
                {
                    u.Id,
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