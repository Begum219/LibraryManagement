using LibraryManagement.Application.DTOs.Auth;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.RateLimiting;

namespace LibraryManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, IUnitOfWork unitOfWork)
        {
            _authService = authService;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Yeni kullanıcı kaydı
        /// </summary>
        [EnableRateLimiting("register")]  // ← EKLENDI
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                _logger.LogInformation("Yeni kullanıcı kaydedildi: {Email}", request.Email);
                return Ok(new { success = true, data = result, message = "Kayıt başarılı!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kayıt hatası: {Email}", request.Email);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı girişi
        /// </summary>
        [EnableRateLimiting("login")]  // ← EKLENDI
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);

                if (result.RequiresTwoFactor)
                {
                    return Ok(new { success = true, requiresTwoFactor = true, message = "2FA kodu gerekli!" });
                }

                _logger.LogInformation("Kullanıcı giriş yaptı: {Email}", request.Email);
                return Ok(new { success = true, data = result, message = "Giriş başarılı!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Giriş hatası: {Email}", request.Email);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Token yenileme
        /// </summary>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request);
                _logger.LogInformation("Token yenilendi");
                return Ok(new { success = true, data = result, message = "Token yenilendi!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token yenileme hatası");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 2FA aktif etme (QR kod alma)
        /// </summary>
        [Authorize]
        [HttpPost("enable-2fa")]
        public async Task<IActionResult> EnableTwoFactor()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var result = await _authService.EnableTwoFactorAsync(userId);
                _logger.LogInformation("2FA aktif edildi: UserId {UserId}", userId);
                return Ok(new { success = true, data = result, message = "QR kodu oluşturuldu. Google Authenticator ile okutun!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA aktifleştirme hatası");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 2FA kod doğrulama ve aktifleştirme
        /// </summary>
        [Authorize]
        [HttpPost("verify-2fa")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] string code)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var isValid = await _authService.VerifyTwoFactorCodeAsync(userId, code);

                if (isValid)
                {
                    _logger.LogInformation("2FA doğrulandı ve aktifleştirildi: UserId {UserId}", userId);
                    return Ok(new { success = true, message = "2FA başarıyla aktifleştirildi!" });
                }

                return BadRequest(new { success = false, message = "Geçersiz kod!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA doğrulama hatası");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 2FA devre dışı bırakma
        /// </summary>
        [Authorize]
        [HttpPost("disable-2fa")]
        public async Task<IActionResult> DisableTwoFactor()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                await _authService.DisableTwoFactorAsync(userId);
                _logger.LogInformation("2FA devre dışı bırakıldı: UserId {UserId}", userId);
                return Ok(new { success = true, message = "2FA devre dışı bırakıldı!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA devre dışı bırakma hatası");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı bilgilerini getir (test için)
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                success = true,
                data = new { userId, email, name, role },
                message = "Kullanıcı bilgileri"
            });
        }

        /// <summary>
        /// 2FA Test - Secret Key ve kod doğrulama
        /// </summary>
        [Authorize]
        [HttpPost("test-2fa")]
        public async Task<IActionResult> Test2FA([FromBody] string code)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var user = await _unitOfWork.Users.GetByIdAsync(userId);

                if (user == null)
                    return BadRequest(new { success = false, message = "Kullanıcı bulunamadı" });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        userId = user.Id,
                        email = user.Email,
                        twoFactorEnabled = user.TwoFactorEnabled,
                        hasSecretKey = !string.IsNullOrEmpty(user.TwoFactorSecretKey),
                        secretKey = user.TwoFactorSecretKey, // ⚠️ Sadece test için!
                        codeToValidate = code
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}