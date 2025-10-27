using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using System.Security.Claims;
using LibraryManagement.Application.Interfaces.Services;

namespace LibraryManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LoanController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LoanController> _logger;
        private readonly ICacheService _cacheService;
        public LoanController(IUnitOfWork unitOfWork, ILogger<LoanController> logger, ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Tüm ödünç kayıtlarını listele (Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpGet]
        public async Task<IActionResult> GetAllLoans()
        {
            try
            {
                var loans = await _unitOfWork.Loans.GetAllAsync();
                return Ok(new { success = true, data = loans });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödünç kayıtları listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            const string cacheKey = "loans:statistics";

            var cached = await _cacheService.GetAsync<object>(cacheKey);
            if (cached != null)
                return Ok(new { success = true, data = cached, source = "cache" });

            // ✅ Önce tüm loan'ları çek
            var allLoans = await _unitOfWork.Loans.GetAllAsync();

            // ✅ Sonra istatistikleri hesapla
            var stats = new
            {
                TotalLoans = allLoans.Count(),
                ActiveLoans = allLoans.Count(l => l.IsReturned == false),
                OverdueLoans = allLoans.Count(l => l.IsReturned == false && l.DueDate < DateTime.UtcNow)
            };

            await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(5));

            return Ok(new { success = true, data = stats, source = "database" });
        }

        /// <summary>
        /// Kullanıcının aktif ödünçlerini getir
        /// </summary>
        [HttpGet("my-loans")]
        public async Task<IActionResult> GetMyLoans()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var myLoans = allLoans.Where(l => l.UserId == userId && l.IsReturned != true).ToList();

                return Ok(new { success = true, data = myLoans });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ödünçleri getirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının geçmiş ödünçlerini getir
        /// </summary>
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var myHistory = allLoans.Where(l => l.UserId == userId && l.IsReturned == true).ToList();

                return Ok(new { success = true, data = myHistory });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ödünç geçmişi getirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gecikmiş ödünçleri listele (Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpGet("overdue")]
        public async Task<IActionResult> GetOverdueLoans()
        {
            try
            {
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var overdueLoans = allLoans.Where(l =>
                    l.IsReturned != true &&
                    l.DueDate < DateTime.UtcNow
                ).ToList();

                return Ok(new { success = true, data = overdueLoans });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gecikmiş ödünçler getirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap ödünç al
        /// </summary>
        [HttpPost("borrow/{bookId}")]
        public async Task<IActionResult> BorrowBook(int bookId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                // Kitap kontrolü
                var book = await _unitOfWork.Books.GetBookWithDetailsAsync(bookId);
                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                // Müsaitlik kontrolü
                if ((book.AvailableCopies ?? 0) <= 0)
                    return BadRequest(new { success = false, message = "Bu kitap şu anda müsait değil" });

                // Kullanıcının aynı kitabı zaten ödünç almış mı kontrolü
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var hasActiveLoans = allLoans.Any(l =>
                    l.UserId == userId &&
                    l.BookId == bookId &&
                    l.IsReturned != true
                );

                if (hasActiveLoans)
                    return BadRequest(new { success = false, message = "Bu kitabı zaten ödünç almışsınız" });

                // Ödünç kaydı oluştur
                var loan = new Loan
                {
                    BookId = bookId,
                    UserId = userId,
                    LoanDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14), // 14 gün ödünç süresi
                    IsReturned = false,
                    Fine = 0,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Loans.AddAsync(loan);

                // Kitap stok güncelleme
                book.AvailableCopies = (book.AvailableCopies ?? 0) - 1;
                book.UpdatedDate = DateTime.UtcNow;
                _unitOfWork.Books.Update(book);

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kitap ödünç alındı: UserId={UserId}, BookId={BookId}", userId, bookId);
                return Ok(new { success = true, data = loan, message = "Kitap başarıyla ödünç alındı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap ödünç alınırken hata oluştu: {BookId}", bookId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap iade et
        /// </summary>
        [HttpPost("return/{loanId}")]
        public async Task<IActionResult> ReturnBook(int loanId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                // Ödünç kaydı kontrolü
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var loan = allLoans.FirstOrDefault(l => l.Id == loanId);

                if (loan == null)
                    return NotFound(new { success = false, message = "Ödünç kaydı bulunamadı" });

                // Kullanıcı kontrolü (admin değilse sadece kendi ödüncünü iade edebilir)
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin" && userRole != "Librarian" && loan.UserId != userId)
                    return Forbid();

                // Zaten iade edilmiş mi kontrolü
                if (loan.IsReturned == true)
                    return BadRequest(new { success = false, message = "Bu kitap zaten iade edilmiş" });

                // İade işlemi
                loan.ReturnDate = DateTime.UtcNow;
                loan.IsReturned = true;
                loan.UpdatedDate = DateTime.UtcNow;

                // Gecikme cezası hesapla (günlük 2 TL)
                if (loan.DueDate < DateTime.UtcNow)
                {
                    var daysLate = (DateTime.UtcNow - loan.DueDate).Days;
                    loan.Fine = daysLate * 2; // 2 TL/gün
                }

                _unitOfWork.Loans.Update(loan);

                // Kitap stok güncelleme
                var book = await _unitOfWork.Books.GetBookWithDetailsAsync(loan.BookId ?? 0);
                if (book != null)
                {
                    book.AvailableCopies = (book.AvailableCopies ?? 0) + 1;
                    book.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.Books.Update(book);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kitap iade edildi: LoanId={LoanId}, Fine={Fine}", loanId, loan.Fine);
                return Ok(new
                {
                    success = true,
                    data = loan,
                    message = (loan.Fine ?? 0) > 0
                        ? $"Kitap iade edildi. Gecikme cezası: {loan.Fine} TL"
                        : "Kitap başarıyla iade edildi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap iade edilirken hata oluştu: {LoanId}", loanId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Ödünç süresini uzat
        /// </summary>
        [HttpPost("renew/{loanId}")]
        public async Task<IActionResult> RenewLoan(int loanId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                // Ödünç kaydı kontrolü
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var loan = allLoans.FirstOrDefault(l => l.Id == loanId);

                if (loan == null)
                    return NotFound(new { success = false, message = "Ödünç kaydı bulunamadı" });

                // Kullanıcı kontrolü
                if (loan.UserId != userId)
                    return Forbid();

                // İade edilmiş mi kontrolü
                if (loan.IsReturned == true)
                    return BadRequest(new { success = false, message = "İade edilmiş kitaplar yenilenemez" });

                // Gecikmiş mi kontrolü
                if (loan.DueDate < DateTime.UtcNow)
                    return BadRequest(new { success = false, message = "Gecikmiş kitaplar yenilenemez. Önce iade edin." });

                // Süre uzatma (14 gün daha)
                loan.DueDate = loan.DueDate.AddDays(14);
                loan.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Loans.Update(loan);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Ödünç süresi uzatıldı: LoanId={LoanId}", loanId);
                return Ok(new { success = true, data = loan, message = "Ödünç süresi 14 gün uzatıldı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödünç süresi uzatılırken hata oluştu: {LoanId}", loanId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Belirli kullanıcının ödünçlerini getir (Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserLoans(int userId)
        {
            try
            {
                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var userLoans = allLoans.Where(l => l.UserId == userId).ToList();

                return Ok(new { success = true, data = userLoans });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ödünçleri getirilirken hata oluştu: {UserId}", userId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}