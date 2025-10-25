using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;

namespace LibraryManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(IUnitOfWork unitOfWork, ILogger<CategoryController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>
        /// Tüm kategorileri listele
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            try
            {
                var categories = await _unitOfWork.Categories.GetAllAsync();
                return Ok(new { success = true, data = categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategoriler listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// ID'ye göre kategori getir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            try
            {
                var categories = await _unitOfWork.Categories.GetAllAsync();
                var category = categories.FirstOrDefault(c => c.Id == id);

                if (category == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                return Ok(new { success = true, data = category });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori getirilirken hata oluştu: {CategoryId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni kategori ekle (Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] Category category)
        {
            try
            {
                category.CreatedDate = DateTime.UtcNow;
                category.IsActive = true;

                await _unitOfWork.Categories.AddAsync(category);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Yeni kategori eklendi: {CategoryName}", category.Name);
                return Ok(new { success = true, data = category, message = "Kategori başarıyla eklendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori eklenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategori güncelle (Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] Category category)
        {
            try
            {
                var categories = await _unitOfWork.Categories.GetAllAsync();
                var existingCategory = categories.FirstOrDefault(c => c.Id == id);

                if (existingCategory == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                // Güncelleme
                existingCategory.Name = category.Name;
                existingCategory.Description = category.Description;
                existingCategory.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Categories.Update(existingCategory);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kategori güncellendi: {CategoryId}", id);
                return Ok(new { success = true, data = existingCategory, message = "Kategori başarıyla güncellendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori güncellenirken hata oluştu: {CategoryId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategori sil (soft delete - Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var categories = await _unitOfWork.Categories.GetAllAsync();
                var category = categories.FirstOrDefault(c => c.Id == id);

                if (category == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                // Kategoriye ait kitap var mı kontrol et
                var booksInCategory = await _unitOfWork.Books.GetBooksByCategoryAsync(id);
                if (booksInCategory.Any())
                {
                    return BadRequest(new { success = false, message = "Bu kategoriye ait kitaplar var. Önce kitapları silin veya başka kategoriye taşıyın." });
                }

                // Soft delete
                category.IsActive = false;
                category.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Categories.Update(category);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kategori silindi (soft delete): {CategoryId}", id);
                return Ok(new { success = true, message = "Kategori başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori silinirken hata oluştu: {CategoryId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategorideki kitap sayısı
        /// </summary>
        [HttpGet("{id}/book-count")]
        public async Task<IActionResult> GetBookCount(int id)
        {
            try
            {
                var books = await _unitOfWork.Books.GetBooksByCategoryAsync(id);
                var count = books.Count();

                return Ok(new { success = true, data = new { categoryId = id, bookCount = count } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori kitap sayısı getirilirken hata oluştu: {CategoryId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategori arama
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchCategories([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new { success = false, message = "Arama kelimesi gerekli" });

                var categories = await _unitOfWork.Categories.FindAsync(c =>
                    c.Name.Contains(query) ||
                    (c.Description != null && c.Description.Contains(query))
                );

                return Ok(new { success = true, data = categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori arama yapılırken hata oluştu: {Query}", query);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}