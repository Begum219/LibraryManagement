using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using System.Security.Claims;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using LibraryManagement.Application.DTOs.Loan;
using LibraryManagement.Application.DTOs.Common;  

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
        /// Tüm ödünç kayıtlarını listele (Pagination ile - Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpGet]
        public async Task<IActionResult> GetAllLoans([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // Parametre kontrolü
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                // IQueryable - Veritabanında sayfalama
                var query = _unitOfWork.Loans.GetAll();
                var totalCount = await query.CountAsync();

                var pagedLoans = await query
                    .OrderByDescending(l => l.LoanDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new
                    {
                        l.PublicId,
                        l.UserId,
                        l.BookId,
                        l.LoanDate,
                        l.DueDate,
                        l.ReturnDate,
                        l.IsReturned,
                        l.Fine,
                        l.RowVersion
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = pagedLoans,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        hasPreviousPage = page > 1,
                        hasNextPage = page < (int)Math.Ceiling(totalCount / (double)pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödünç kayıtları listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Ödünç istatistikleri (cache'li)
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            const string cacheKey = "loans:statistics";

            var cached = await _cacheService.GetAsync<object>(cacheKey);
            if (cached != null)
                return Ok(new { success = true, data = cached, source = "cache" });

            var allLoans = await _unitOfWork.Loans.GetAllAsync();

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
        /// PublicId ile ödünç kaydı getir (IDOR korumalı)
        /// </summary>
        [HttpGet("{publicId}")]
        public async Task<IActionResult> GetLoanByPublicId(Guid publicId)
        {
            try
            {
                var loan = await _unitOfWork.Loans.GetLoanWithDetailsByPublicIdAsync(publicId);

                if (loan == null)
                    return NotFound(new { success = false, message = "Ödünç kaydı bulunamadı" });

                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // IDOR KONTROLÜ
                if (loan.UserId != currentUserId &&
                    currentUserRole != "Admin" &&
                    currentUserRole != "Librarian")
                {
                    _logger.LogWarning("IDOR denemesi: Kullanıcı {CurrentUserId} başkasının ödüncüne erişmeye çalıştı: {LoanId}",
                        currentUserId, loan.Id);
                    return Forbid();
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        loan.PublicId,
                        loan.UserId,
                        loan.BookId,
                        loan.LoanDate,
                        loan.DueDate,
                        loan.ReturnDate,
                        loan.IsReturned,
                        loan.Fine,
                        loan.RowVersion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödünç kaydı getirme hatası: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının aktif ödünçlerini getir (Pagination ile)
        /// </summary>
        [HttpGet("my-loans")]
        public async Task<IActionResult> GetMyLoans([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var query = _unitOfWork.Loans.GetAll()
                    .Where(l => l.UserId == userId && l.IsReturned != true);

                var totalCount = await query.CountAsync();

                var myLoans = await query
                    .OrderByDescending(l => l.LoanDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new
                    {
                        l.PublicId,
                        l.BookId,
                        l.LoanDate,
                        l.DueDate,
                        l.IsReturned,
                        l.Fine,
                        l.RowVersion
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = myLoans,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        hasPreviousPage = page > 1,
                        hasNextPage = page < (int)Math.Ceiling(totalCount / (double)pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ödünçleri getirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının geçmiş ödünçlerini getir (Pagination ile)
        /// </summary>
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var query = _unitOfWork.Loans.GetAll()
                    .Where(l => l.UserId == userId && l.IsReturned == true);

                var totalCount = await query.CountAsync();

                var myHistory = await query
                    .OrderByDescending(l => l.ReturnDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new
                    {
                        l.PublicId,
                        l.BookId,
                        l.LoanDate,
                        l.DueDate,
                        l.ReturnDate,
                        l.Fine,
                        l.RowVersion
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = myHistory,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        hasPreviousPage = page > 1,
                        hasNextPage = page < (int)Math.Ceiling(totalCount / (double)pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ödünç geçmişi getirilirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gecikmiş ödünçleri listele (Pagination ile - Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpGet("overdue")]
        public async Task<IActionResult> GetOverdueLoans([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var query = _unitOfWork.Loans.GetAll()
                    .Where(l => l.IsReturned != true && l.DueDate < DateTime.UtcNow);

                var totalCount = await query.CountAsync();

                var overdueLoans = await query
                    .OrderBy(l => l.DueDate)  // En eski gecikme önce
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new
                    {
                        l.PublicId,
                        l.UserId,
                        l.BookId,
                        l.LoanDate,
                        l.DueDate,
                        DaysOverdue = (DateTime.UtcNow - l.DueDate).Days,
                        l.RowVersion
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = overdueLoans,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        hasPreviousPage = page > 1,
                        hasNextPage = page < (int)Math.Ceiling(totalCount / (double)pageSize)
                    }
                });
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

                var book = await _unitOfWork.Books.GetBookWithDetailsAsync(bookId);
                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                if ((book.AvailableCopies ?? 0) <= 0)
                    return BadRequest(new { success = false, message = "Bu kitap şu anda müsait değil" });

                var allLoans = await _unitOfWork.Loans.GetAllAsync();
                var hasActiveLoans = allLoans.Any(l =>
                    l.UserId == userId &&
                    l.BookId == bookId &&
                    l.IsReturned != true
                );

                if (hasActiveLoans)
                    return BadRequest(new { success = false, message = "Bu kitabı zaten ödünç almışsınız" });

                var loan = new Loan
                {
                    PublicId = Guid.NewGuid(),
                    BookId = bookId,
                    UserId = userId,
                    LoanDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14),
                    IsReturned = false,
                    Fine = 0,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Loans.AddAsync(loan);

                book.AvailableCopies = (book.AvailableCopies ?? 0) - 1;
                book.UpdatedDate = DateTime.UtcNow;
                _unitOfWork.Books.Update(book);

                await _unitOfWork.SaveChangesAsync();
                await _cacheService.RemoveAsync("loans:statistics");

                _logger.LogInformation("Kitap ödünç alındı: UserId={UserId}, BookId={BookId}", userId, bookId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        loan.PublicId,
                        loan.LoanDate,
                        loan.DueDate,
                        loan.RowVersion
                    },
                    message = "Kitap başarıyla ödünç alındı"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap ödünç alınırken hata oluştu: {BookId}", bookId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap iade et (DTO kullanarak - Optimistic Locking)
        /// </summary>
        [HttpPost("return/{publicId}")]
        public async Task<IActionResult> ReturnBook(Guid publicId, [FromBody] ReturnBookDto dto)  // ✅ DTO
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                var loan = await _unitOfWork.Loans.GetByPublicIdAsync(publicId);

                if (loan == null)
                    return NotFound(new { success = false, message = "Ödünç kaydı bulunamadı" });

                // IDOR KONTROLÜ
                if (userRole != "Admin" && userRole != "Librarian" && loan.UserId != userId)
                {
                    _logger.LogWarning("IDOR denemesi: Kullanıcı {CurrentUserId} başkasının ödüncünü iade etmeye çalıştı: {LoanId}",
                        userId, loan.Id);
                    return Forbid();
                }

                if (loan.IsReturned == true)
                    return BadRequest(new { success = false, message = "Bu kitap zaten iade edilmiş" });

                // RowVersion kontrolü (Optimistic Locking)
                if (dto?.RowVersion != null && dto.RowVersion.Length > 0)
                {
                    _unitOfWork.SetOriginalRowVersion(loan, dto.RowVersion);
                }

                // İade işlemi
                loan.ReturnDate = DateTime.UtcNow;
                loan.IsReturned = true;
                loan.UpdatedDate = DateTime.UtcNow;

                // Gecikme cezası hesapla
                if (loan.DueDate < DateTime.UtcNow)
                {
                    var daysLate = (DateTime.UtcNow - loan.DueDate).Days;
                    loan.Fine = daysLate * 2;
                }

                _unitOfWork.Loans.Update(loan);

                // Kitap stok güncelleme
                var book = await _unitOfWork.Books.GetByIdAsync(loan.BookId ?? 0);
                if (book != null)
                {
                    book.AvailableCopies = (book.AvailableCopies ?? 0) + 1;
                    book.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.Books.Update(book);
                }

                // CONCURRENCY EXCEPTION YAKALA
                try
                {
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex,
                        "Concurrency conflict: Loan {LoanPublicId} or Book modified by another process",
                        publicId);

                    return Conflict(new
                    {
                        success = false,
                        message = "Bu kayıt başka bir işlem tarafından güncellenmiş. Lütfen sayfayı yenileyin.",
                        errorCode = "CONCURRENCY_CONFLICT"
                    });
                }

                await _cacheService.RemoveAsync("loans:statistics");

                _logger.LogInformation("Kitap iade edildi: LoanId={LoanId}, Fine={Fine}", loan.Id, loan.Fine);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        loan.PublicId,
                        loan.ReturnDate,
                        loan.Fine,
                        loan.RowVersion
                    },
                    message = (loan.Fine ?? 0) > 0
                        ? $"Kitap iade edildi. Gecikme cezası: {loan.Fine} TL"
                        : "Kitap başarıyla iade edildi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap iade edilirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Ödünç süresini uzat (DTO kullanarak - Optimistic Locking)
        /// </summary>
        [HttpPost("renew/{publicId}")]
        public async Task<IActionResult> RenewLoan(Guid publicId, [FromBody] RenewLoanDto dto)  
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var loan = await _unitOfWork.Loans.GetByPublicIdAsync(publicId);

                if (loan == null)
                    return NotFound(new { success = false, message = "Ödünç kaydı bulunamadı" });

                // IDOR KONTROLÜ
                if (loan.UserId != userId)
                {
                    _logger.LogWarning("IDOR denemesi: Kullanıcı {CurrentUserId} başkasının ödüncünü yenilemeye çalıştı: {LoanId}",
                        userId, loan.Id);
                    return Forbid();
                }

                if (loan.IsReturned == true)
                    return BadRequest(new { success = false, message = "İade edilmiş kitaplar yenilenemez" });

                if (loan.DueDate < DateTime.UtcNow)
                    return BadRequest(new { success = false, message = "Gecikmiş kitaplar yenilenemez. Önce iade edin." });

                // RowVersion kontrolü (Optimistic Locking)
                if (dto?.RowVersion != null && dto.RowVersion.Length > 0)
                {
                    _unitOfWork.SetOriginalRowVersion(loan, dto.RowVersion);
                }

                // Süre uzatma
                loan.DueDate = loan.DueDate.AddDays(14);
                loan.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Loans.Update(loan);

                try
                {
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex,
                        "Concurrency conflict: Loan {LoanPublicId} modified by another process",
                        publicId);

                    return Conflict(new
                    {
                        success = false,
                        message = "Bu kayıt başka bir işlem tarafından güncellenmiş. Lütfen sayfayı yenileyin.",
                        errorCode = "CONCURRENCY_CONFLICT"
                    });
                }

                _logger.LogInformation("Ödünç süresi uzatıldı: LoanId={LoanId}", loan.Id);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        loan.PublicId,
                        loan.DueDate,
                        loan.RowVersion
                    },
                    message = "Ödünç süresi 14 gün uzatıldı"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödünç süresi uzatılırken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Belirli kullanıcının ödünçlerini getir (Pagination ile - Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserLoans(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var query = _unitOfWork.Loans.GetAll()
                    .Where(l => l.UserId == userId);

                var totalCount = await query.CountAsync();

                var userLoans = await query
                    .OrderByDescending(l => l.LoanDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new
                    {
                        l.PublicId,
                        l.BookId,
                        l.LoanDate,
                        l.DueDate,
                        l.ReturnDate,
                        l.IsReturned,
                        l.Fine,
                        l.RowVersion
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = userLoans,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        hasPreviousPage = page > 1,
                        hasNextPage = page < (int)Math.Ceiling(totalCount / (double)pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı ödünçleri getirilirken hata oluştu: {UserId}", userId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}