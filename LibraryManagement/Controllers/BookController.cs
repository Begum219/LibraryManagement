using LibraryManagement.Application.Interfaces.UnitOfWork;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Domain.Entities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using LibraryManagement.Application.DTOs.Book;

namespace LibraryManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;
        private readonly ILogger<BookController> _logger;

        public BookController(
            IUnitOfWork unitOfWork,
            ICacheService cacheService,
            ILogger<BookController> logger)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Tüm kitapları listele (Pagination + Search + Filters)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllBooks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isAvailable = null,
            [FromQuery] string sortBy = "CreatedDate",
            [FromQuery] string sortOrder = "desc")
        {
            try
            {
                // Pagination validasyonu
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                // Base query
                var query = _unitOfWork.Books.GetAll();

                // 🔍 SEARCH FİLTRESİ (Title, Author, ISBN, Publisher)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(b =>
                        b.Title.ToLower().Contains(search) ||
                        b.Author.ToLower().Contains(search) ||
                        (b.Isbn != null && b.Isbn.ToLower().Contains(search)) ||
                        (b.Publisher != null && b.Publisher.ToLower().Contains(search))
                    );
                }

                // 📚 KATEGORİ FİLTRESİ
                if (categoryId.HasValue)
                {
                    query = query.Where(b => b.CategoryId == categoryId.Value);
                }

                // ✅ AKTİFLİK FİLTRESİ
                if (isActive.HasValue)
                {
                    query = query.Where(b => b.IsActive == isActive.Value);
                }

                // 📖 MÜSAİTLİK FİLTRESİ (Stokta olan kitaplar)
                if (isAvailable.HasValue && isAvailable.Value)
                {
                    query = query.Where(b => b.AvailableCopies > 0);
                }

                // 📊 SIRALAMA
                query = sortBy.ToLower() switch
                {
                    "title" => sortOrder.ToLower() == "asc"
                        ? query.OrderBy(b => b.Title)
                        : query.OrderByDescending(b => b.Title),

                    "author" => sortOrder.ToLower() == "asc"
                        ? query.OrderBy(b => b.Author)
                        : query.OrderByDescending(b => b.Author),

                    "publishyear" => sortOrder.ToLower() == "asc"
                        ? query.OrderBy(b => b.PublishYear)
                        : query.OrderByDescending(b => b.PublishYear),

                    "availablecopies" => sortOrder.ToLower() == "asc"
                        ? query.OrderBy(b => b.AvailableCopies)
                        : query.OrderByDescending(b => b.AvailableCopies),

                    "createddate" or _ => sortOrder.ToLower() == "asc"
                        ? query.OrderBy(b => b.CreatedDate)
                        : query.OrderByDescending(b => b.CreatedDate)
                };

                // Toplam kayıt sayısı (filtrelenmiş)
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Sayfalama
                var pagedBooks = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(b => new
                    {
                        b.PublicId,
                        b.Title,
                        b.Author,
                        b.Isbn,
                        b.Publisher,
                        b.PublishYear,
                        b.CategoryId,
                        b.TotalCopies,
                        b.AvailableCopies,
                        b.IsActive
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = pagedBooks,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages,
                        hasPreviousPage = page > 1,
                        hasNextPage = page < totalPages
                    },
                    filters = new
                    {
                        search,
                        categoryId,
                        isActive,
                        isAvailable,
                        sortBy,
                        sortOrder
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitaplar listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// PublicId ile kitap getir (detaylı - kategori ve ödünç bilgileri ile)
        /// </summary>
        [HttpGet("{publicId:guid}")]
        public async Task<IActionResult> GetBookByPublicId(Guid publicId)
        {
            try
            {
                var book = await _unitOfWork.Books.GetBookWithDetailsByPublicIdAsync(publicId);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                var bookWithPublicId = new
                {
                    book.PublicId,
                    book.Title,
                    book.Author,
                    book.Isbn,
                    book.Publisher,
                    book.PublishYear,
                    book.CategoryId,
                    Category = book.Category != null ? new
                    {
                        book.Category.Id,
                        book.Category.PublicId,
                        book.Category.Name,
                        book.Category.Description
                    } : null,
                    book.TotalCopies,
                    book.AvailableCopies,
                    book.IsActive,
                    book.CreatedDate,
                    book.UpdatedDate,
                    RowVersion = book.RowVersion
                };

                return Ok(new { success = true, data = bookWithPublicId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap detayı getirilirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategoriye göre kitapları listele
        /// </summary>
        [HttpGet("category/{categoryId}")]
        public async Task<IActionResult> GetBooksByCategory(int categoryId)
        {
            try
            {
                var books = await _unitOfWork.Books.GetBooksByCategoryAsync(categoryId);

                var booksWithPublicId = books.Select(b => new
                {
                    b.PublicId,
                    b.Title,
                    b.Author,
                    b.Isbn,
                    b.Publisher,
                    b.PublishYear,
                    b.CategoryId,
                    b.TotalCopies,
                    b.AvailableCopies,
                    b.IsActive
                });

                return Ok(new { success = true, data = booksWithPublicId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategoriye göre kitaplar getirilirken hata oluştu: {CategoryId}", categoryId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap müsaitlik kontrolü (PublicId ile)
        /// </summary>
        [HttpGet("{publicId}/availability")]
        public async Task<IActionResult> CheckAvailability(Guid publicId)
        {
            try
            {
                var book = await _unitOfWork.Books.GetByPublicIdAsync(publicId);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                var isAvailable = (book.AvailableCopies ?? 0) > 0;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        bookPublicId = publicId,
                        isAvailable,
                        availableCopies = book.AvailableCopies
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müsaitlik kontrolü yapılırken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni kitap ekle (Admin/Librarian) - ISBN kontrolü + Cache temizleme
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPost]
        public async Task<IActionResult> CreateBook([FromBody] CreateBookDto dto)
        {
            try
            {
                // ISBN kontrolü - Aynı ISBN varsa engelle
                if (!string.IsNullOrWhiteSpace(dto.Isbn))
                {
                    var existingBook = await _unitOfWork.Books
                        .FindAsync(b => b.Isbn == dto.Isbn && !b.IsDeleted);

                    if (existingBook.Any())
                    {
                        var existing = existingBook.First();
                        _logger.LogWarning("Duplicate ISBN attempted: {ISBN} for book {Title}",
                            dto.Isbn, dto.Title);

                        return BadRequest(new
                        {
                            success = false,
                            message = $"Bu ISBN ({dto.Isbn}) zaten kayıtlı! Mevcut kitap: {existing.Title}",
                            errorCode = "DUPLICATE_ISBN",
                            existingBook = new
                            {
                                existing.PublicId,
                                existing.Title,
                                existing.Author
                            }
                        });
                    }
                }

                var book = new Book
                {
                    PublicId = Guid.NewGuid(),
                    Title = dto.Title,
                    Author = dto.Author,
                    Isbn = dto.Isbn,
                    Publisher = dto.Publisher,
                    PublishYear = dto.PublishYear,
                    CategoryId = dto.CategoryId,
                    TotalCopies = dto.TotalCopies,
                    AvailableCopies = dto.TotalCopies,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Books.AddAsync(book);
                await _unitOfWork.SaveChangesAsync();

                // Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Yeni kitap eklendi: {BookTitle}, ISBN: {ISBN}",
                    book.Title, book.Isbn);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        book.PublicId,
                        book.Title,
                        book.Author,
                        book.Isbn
                    },
                    message = "Kitap başarıyla eklendi"
                });
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Books_ISBN") == true)
            {
                _logger.LogError(ex, "ISBN unique constraint violation: {ISBN}", dto.Isbn);
                return BadRequest(new
                {
                    success = false,
                    message = $"Bu ISBN ({dto.Isbn}) zaten kayıtlı!",
                    errorCode = "DUPLICATE_ISBN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap eklenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap güncelle (Admin/Librarian) - ISBN kontrolü + Optimistic Locking
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPut("{publicId}")]
        public async Task<IActionResult> UpdateBook(Guid publicId, [FromBody] UpdateBookDto dto)
        {
            try
            {
                var existingBook = await _unitOfWork.Books.GetBookWithDetailsByPublicIdAsync(publicId);

                if (existingBook == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                // ISBN değiştiriliyorsa ve yeni ISBN başka kitapta varsa engelle
                if (!string.IsNullOrWhiteSpace(dto.Isbn) && existingBook.Isbn != dto.Isbn)
                {
                    var duplicateBook = await _unitOfWork.Books
                        .FindAsync(b => b.Isbn == dto.Isbn && b.Id != existingBook.Id && !b.IsDeleted);

                    if (duplicateBook.Any())
                    {
                        var duplicate = duplicateBook.First();
                        _logger.LogWarning("Duplicate ISBN update attempted: {ISBN}", dto.Isbn);

                        return BadRequest(new
                        {
                            success = false,
                            message = $"Bu ISBN ({dto.Isbn}) başka bir kitapta kayıtlı: {duplicate.Title}",
                            errorCode = "DUPLICATE_ISBN"
                        });
                    }
                }

                // RowVersion'ı set et
                if (dto.RowVersion != null)
                {
                    _unitOfWork.SetOriginalRowVersion(existingBook, dto.RowVersion);
                }

                // Güncelleme işlemleri
                existingBook.Title = dto.Title;
                existingBook.Author = dto.Author;
                existingBook.Isbn = dto.Isbn;
                existingBook.Publisher = dto.Publisher;
                existingBook.PublishYear = dto.PublishYear;
                existingBook.CategoryId = dto.CategoryId;
                existingBook.TotalCopies = dto.TotalCopies;
                existingBook.AvailableCopies = dto.AvailableCopies;
                existingBook.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Books.Update(existingBook);

                try
                {
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex,
                        "Concurrency conflict: Book {PublicId} modified by another user",
                        publicId);

                    return Conflict(new
                    {
                        success = false,
                        message = "Bu kitap başka bir kullanıcı tarafından güncellenmiş.",
                        errorCode = "CONCURRENCY_CONFLICT"
                    });
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Books_ISBN") == true)
                {
                    _logger.LogError(ex, "ISBN unique constraint violation during update");
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Bu ISBN ({dto.Isbn}) zaten kayıtlı!",
                        errorCode = "DUPLICATE_ISBN"
                    });
                }

                // Cache temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Kitap güncellendi: {PublicId}", publicId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        existingBook.PublicId,
                        existingBook.Title,
                        existingBook.Author,
                        existingBook.Isbn,
                        existingBook.RowVersion
                    },
                    message = "Kitap başarıyla güncellendi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap güncellenirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap sil (soft delete - Admin/Librarian - PublicId ile)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpDelete("{publicId:guid}")]
        public async Task<IActionResult> DeleteBook(Guid publicId)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var book = await _unitOfWork.Books.GetByPublicIdAsync(publicId);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                await _unitOfWork.Books.SoftDeleteAsync(book, currentUserId);
                await _unitOfWork.SaveChangesAsync();

                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Kitap soft delete edildi: {PublicId}, DeletedBy={DeletedBy}",
                    publicId, currentUserId);

                return Ok(new { success = true, message = "Kitap başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap silinirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap arama (title, author, isbn)
        /// </summary>
        [EnableRateLimiting("search")]
        [HttpGet("search")]
        public async Task<IActionResult> SearchBooks([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new { success = false, message = "Arama kelimesi gerekli" });

                var books = await _unitOfWork.Books.FindAsync(b =>
                    b.Title.Contains(query) ||
                    b.Author.Contains(query) ||
                    (b.Isbn != null && b.Isbn.Contains(query))
                );

                // PublicId göster
                var booksWithPublicId = books.Select(b => new
                {
                    b.PublicId,
                    b.Title,
                    b.Author,
                    b.Isbn,
                    b.Publisher,
                    b.PublishYear,
                    b.TotalCopies,
                    b.AvailableCopies,
                    b.IsActive
                });

                return Ok(new { success = true, data = booksWithPublicId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap arama yapılırken hata oluştu: {Query}", query);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}