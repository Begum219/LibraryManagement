using LibraryManagement.Application.Interfaces.UnitOfWork;
using LibraryManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                    return Ok(new { success = true, data = cachedBooks, source = "cache" });
                }

                // ✅ Cache'te yoksa veritabanından getir
                var books = await _unitOfWork.Books.GetAllAsync();

                // ✅ Cache'e kaydet (10 dakika)
                await _cacheService.SetAsync(cacheKey, books.ToList(), TimeSpan.FromMinutes(10));

                _logger.LogInformation("Kitaplar veritabanından geldi ve cache'lendi");
                return Ok(new { success = true, data = books, source = "database" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitaplar listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// ID'ye göre kitap getir (detaylı - kategori ve ödünç bilgileri ile)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookById(int id)
        {
            try
            {
                var book = await _unitOfWork.Books.GetBookWithDetailsAsync(id);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                return Ok(new { success = true, data = book });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap detayı getirilirken hata oluştu: {BookId}", id);
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
                return Ok(new { success = true, data = books });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategoriye göre kitaplar getirilirken hata oluştu: {CategoryId}", categoryId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap müsaitlik kontrolü
        /// </summary>
        [HttpGet("{id}/availability")]
        public async Task<IActionResult> CheckAvailability(int id)
        {
            try
            {
                var isAvailable = await _unitOfWork.Books.IsBookAvailableAsync(id);
                return Ok(new { success = true, data = new { bookId = id, isAvailable } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müsaitlik kontrolü yapılırken hata oluştu: {BookId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni kitap ekle (Admin/Librarian) - Cache temizleme ile
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPost]
        public async Task<IActionResult> CreateBook([FromBody] Book book)
        {
            try
            {
                book.CreatedDate = DateTime.UtcNow;
                book.IsActive = true;

                await _unitOfWork.Books.AddAsync(book);
                await _unitOfWork.SaveChangesAsync();

                // ✅ Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Yeni kitap eklendi ve cache temizlendi: {BookTitle}", book.Title);
                return Ok(new { success = true, data = book, message = "Kitap başarıyla eklendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap eklenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap güncelle (Admin/Librarian) - Cache temizleme ile
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBook(int id, [FromBody] Book book)
        {
            try
            {
                var existingBook = await _unitOfWork.Books.GetBookWithDetailsAsync(id);

                if (existingBook == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                // Güncelleme
                existingBook.Title = book.Title;
                existingBook.Author = book.Author;
                existingBook.Isbn = book.Isbn;
                existingBook.Publisher = book.Publisher;
                existingBook.PublishYear = book.PublishYear;
                existingBook.CategoryId = book.CategoryId;
                existingBook.TotalCopies = book.TotalCopies;
                existingBook.AvailableCopies = book.AvailableCopies;
                existingBook.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Books.Update(existingBook);
                await _unitOfWork.SaveChangesAsync();

                // ✅ Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Kitap güncellendi ve cache temizlendi: {BookId}", id);
                return Ok(new { success = true, data = existingBook, message = "Kitap başarıyla güncellendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap güncellenirken hata oluştu: {BookId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap sil (soft delete - Admin) - Cache temizleme ile
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            try
            {
                var book = await _unitOfWork.Books.GetBookWithDetailsAsync(id);

                if (book == null)
                    return NotFound(new { success = false, message = "Kitap bulunamadı" });

                // Soft delete
                book.IsActive = false;
                book.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Books.Update(book);
                await _unitOfWork.SaveChangesAsync();

                // ✅ Cache'i temizle
                await _cacheService.RemoveByPrefixAsync("books:");

                _logger.LogInformation("Kitap silindi (soft delete) ve cache temizlendi: {BookId}", id);
                return Ok(new { success = true, message = "Kitap başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap silinirken hata oluştu: {BookId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kitap arama (title, author, isbn)
        /// </summary>
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

                return Ok(new { success = true, data = books });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kitap arama yapılırken hata oluştu: {Query}", query);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}