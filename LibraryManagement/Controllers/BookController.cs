using LibraryManagement.Application.Interfaces.UnitOfWork;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Domain.Entities;
using System.Security.Claims;

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
        /// Tüm kitapları listele (CACHE'li)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllBooks()
        {
            try
            {
                const string cacheKey = "books:all";

                // ✅ Önce cache'e bak
                var cachedBooks = await _cacheService.GetAsync<List<Book>>(cacheKey);

                if (cachedBooks != null)
                {
                    _logger.LogInformation("Kitaplar cache'ten geldi");

                    // PublicId göster, Id gizle
                    var booksWithPublicId = cachedBooks.Select(b => new
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

                    return Ok(new { success = true, data = booksWithPublicId, source = "cache" });
                }

                // ✅ Cache'te yoksa veritabanından getir
                var books = await _unitOfWork.Books.GetAllAsync();

                // ✅ Cache'e kaydet (10 dakika)
                await _cacheService.SetAsync(cacheKey, books.ToList(), TimeSpan.FromMinutes(10));

                _logger.LogInformation("Kitaplar veritabanından geldi ve cache'lendi");

                // PublicId göster
                var booksListWithPublicId = books.Select(b => new
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

                return Ok(new { success = true, data = booksListWithPublicId, source = "database" });
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
        [HttpGet("{publicId}")]
        public async Task<IActionResult> GetBookByPublicId(Guid publicId)
        {
            try
            {
                // PublicId'den kitabı bul
                var book = await _unitOfWork.Books.GetBookWithDetailsByPublicIdAsync(publicId);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                // PublicId göster
                var bookWithPublicId = new
                {
                    book.PublicId,
                    book.Title,
                    book.Author,
                    book.Isbn,
                    book.Publisher,
                    book.PublishYear,
                    book.CategoryId,
                    book.Category,
                    book.TotalCopies,
                    book.AvailableCopies,
                    book.IsActive,
                    book.CreatedDate,
                    book.UpdatedDate
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

                // PublicId göster
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
        /// Yeni kitap ekle (Admin/Librarian) - Cache temizleme ile
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPost]
        public async Task<IActionResult> CreateBook([FromBody] CreateBookDto dto)
        {
            try
            {
                var book = new Book
                {
                    PublicId = Guid.NewGuid(),  // ← YENİ GUID
                    Title = dto.Title,
                    Author = dto.Author,
                    Isbn = dto.Isbn,
                    Publisher = dto.Publisher,
                    PublishYear = dto.PublishYear,
                    CategoryId = dto.CategoryId,
                    TotalCopies = dto.TotalCopies,
                    AvailableCopies = dto.TotalCopies,  // Başlangıçta tümü müsait
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Books.AddAsync(book);
                await _unitOfWork.SaveChangesAsync();

                // ✅ Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Yeni kitap eklendi ve cache temizlendi: {BookTitle}", book.Title);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        book.PublicId,
                        book.Title,
                        book.Author
                    },
                    message = "Kitap başarıyla eklendi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap eklenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap güncelle (Admin/Librarian - PublicId ile) - Cache temizleme ile
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

                // Güncelleme
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
                await _unitOfWork.SaveChangesAsync();

                // ✅ Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Kitap güncellendi ve cache temizlendi: {PublicId}", publicId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        existingBook.PublicId,
                        existingBook.Title,
                        existingBook.Author
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
        /// Kitap sil (soft delete - Admin - PublicId ile) - Cache temizleme ile
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpDelete("{publicId:guid}")]
        public async Task<IActionResult> DeleteBook(Guid publicId)
        {
            try
            {
                // ✅ Mevcut kullanıcının ID'sini al
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var book = await _unitOfWork.Books.GetByPublicIdAsync(publicId);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                // ✅ YENİ: SoftDeleteAsync metodunu kullan
                await _unitOfWork.Books.SoftDeleteAsync(book, currentUserId);
                await _unitOfWork.SaveChangesAsync();

                // ✅ Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Kitap soft delete edildi: BookId={BookId}, DeletedBy={DeletedBy}",
                    book.Id, currentUserId);

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

    // DTOs
    public class CreateBookDto
    {
        public string Title { get; set; } = null!;
        public string Author { get; set; } = null!;
        public string? Isbn { get; set; }
        public string? Publisher { get; set; }
        public int? PublishYear { get; set; }
        public int CategoryId { get; set; }
        public int TotalCopies { get; set; }
    }

    public class UpdateBookDto
    {
        public string Title { get; set; } = null!;
        public string Author { get; set; } = null!;
        public string? Isbn { get; set; }
        public string? Publisher { get; set; }
        public int? PublishYear { get; set; }
        public int CategoryId { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
    }
}