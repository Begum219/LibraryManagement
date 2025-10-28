using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;

namespace Infrastructure.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Swagger ve statik dosyaları loglamayalım
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.Value?.Contains("favicon") == true ||
                context.Request.Path.Value?.Contains(".css") == true ||
                context.Request.Path.Value?.Contains(".js") == true ||
                context.Request.Path.Value?.Contains(".png") == true)
            {
                await _next(context);
                return;
            }

            var startTime = DateTime.Now;
            var stopwatch = Stopwatch.StartNew();

            var method = context.Request.Method;
            var path = context.Request.Path;
            var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";
            var userEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "Anonim";
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "-";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Bilinmiyor";

            // ✅ BAŞLANGIÇ LOGU
            _logger.LogInformation(@"
========================================
📋 İSTEK BAŞLADI
Zaman: {StartTime:yyyy-MM-dd HH:mm:ss.fff}
Method: {Method}
Path: {Path}{QueryString}
Kullanıcı: {UserEmail} (ID: {UserId})
IP Adresi: {IpAddress}
========================================",
                startTime, method, path, queryString, userEmail, userId, ipAddress);

            string? errorMessage = null;
            Exception? exception = null;

            try
            {
                // İsteği işle
                await _next(context);
            }
            catch (Exception ex)
            {
                exception = ex;
                errorMessage = ex.Message;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var endTime = DateTime.Now;
                var duration = stopwatch.ElapsedMilliseconds;
                var statusCode = context.Response.StatusCode;

                // ✅ BİTİŞ LOGU
                if (exception == null && statusCode >= 200 && statusCode < 400)
                {
                    _logger.LogInformation(@"
========================================
✅ İSTEK BAŞARIYLA TAMAMLANDI
Başlangıç: {StartTime:yyyy-MM-dd HH:mm:ss.fff}
Bitiş: {EndTime:yyyy-MM-dd HH:mm:ss.fff}
Toplam Süre: {Duration}ms
Method: {Method}
Path: {Path}{QueryString}
Status: {StatusCode}
Kullanıcı: {UserEmail}
Hata: YOK
========================================",
                        startTime, endTime, duration, method, path, queryString, statusCode, userEmail);
                }
                else if (statusCode >= 400 && statusCode < 500)
                {
                    _logger.LogWarning(@"
========================================
⚠️ İSTEK TAMAMLANDI (İstemci Hatası)
Başlangıç: {StartTime:yyyy-MM-dd HH:mm:ss.fff}
Bitiş: {EndTime:yyyy-MM-dd HH:mm:ss.fff}
Toplam Süre: {Duration}ms
Method: {Method}
Path: {Path}{QueryString}
Status: {StatusCode}
Kullanıcı: {UserEmail}
Hata Mesajı: {ErrorMessage}
========================================",
                        startTime, endTime, duration, method, path, queryString, statusCode, userEmail, errorMessage ?? "İstemci hatası");
                }
                else if (exception != null || statusCode >= 500)
                {
                    _logger.LogError(exception, @"
========================================
❌ İSTEK BAŞARISIZ (Sunucu Hatası)
Başlangıç: {StartTime:yyyy-MM-dd HH:mm:ss.fff}
Bitiş: {EndTime:yyyy-MM-dd HH:mm:ss.fff}
Toplam Süre: {Duration}ms
Method: {Method}
Path: {Path}{QueryString}
Status: {StatusCode}
Kullanıcı: {UserEmail}
Hata Mesajı: {ErrorMessage}
Exception Type: {ExceptionType}
========================================",
                        startTime, endTime, duration, method, path, queryString, statusCode, userEmail,
                        errorMessage, exception?.GetType().Name);
                }
            }
        }
    }
}