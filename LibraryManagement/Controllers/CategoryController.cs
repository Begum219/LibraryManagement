using LibraryManagement.Application.Interfaces.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LibraryManagement.Application.DTOs.Category;  

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
        /// Tüm kategorileri listele (Pagination ile)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllCategories([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // Parametre kontrolü
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                // IQueryable - Veritabanında sayfalama
                var query = _unitOfWork.Categories.GetAll();
                var totalCount = await query.CountAsync();

                var pagedCategories = await query
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.PublicId,
                        c.Name,
                        c.Description,
                        c.IsActive,
                        c.CreatedDate,
                        c.RowVersion
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = pagedCategories,
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
                _logger.LogError(ex, "Kategoriler listelenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// PublicId ile kategori getir
        /// </summary>
        [HttpGet("{publicId:guid}")]
        public async Task<IActionResult> GetCategoryByPublicId(Guid publicId)
        {
            try
            {
                var query = _unitOfWork.Categories.GetAll();
                var category = await query.FirstOrDefaultAsync(c => c.PublicId == publicId);

                if (category == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        category.PublicId,
                        category.Name,
                        category.Description,
                        category.IsActive,
                        category.CreatedDate,
                        category.UpdatedDate,
                        category.RowVersion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori getirilirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni kategori ekle (Admin/Librarian)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            try
            {
                var category = new Category
                {
                    PublicId = Guid.NewGuid(),
                    Name = dto.Name,
                    Description = dto.Description,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Categories.AddAsync(category);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Yeni kategori eklendi: {CategoryName}", category.Name);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        category.PublicId,
                        category.Name,
                        category.Description,
                        category.RowVersion
                    },
                    message = "Kategori başarıyla eklendi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori eklenirken hata oluştu");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategori güncelle (Admin/Librarian - Optimistic Locking)
        /// </summary>
        [Authorize(Roles = "Admin,Librarian")]
        [HttpPut("{publicId:guid}")]
        public async Task<IActionResult> UpdateCategory(Guid publicId, [FromBody] UpdateCategoryDto dto)
        {
            try
            {
                var query = _unitOfWork.Categories.GetAll();
                var existingCategory = await query.FirstOrDefaultAsync(c => c.PublicId == publicId);

                if (existingCategory == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                // RowVersion kontrolü (Optimistic Locking)
                if (dto.RowVersion != null)
                {
                    _unitOfWork.SetOriginalRowVersion(existingCategory, dto.RowVersion);
                }

                // Güncelleme
                existingCategory.Name = dto.Name;
                existingCategory.Description = dto.Description;
                existingCategory.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Categories.Update(existingCategory);

                try
                {
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex,
                        "Concurrency conflict: Category {PublicId} modified by another user",
                        publicId);

                    return Conflict(new
                    {
                        success = false,
                        message = "Bu kategori başka bir kullanıcı tarafından güncellenmiş.",
                        errorCode = "CONCURRENCY_CONFLICT"
                    });
                }

                _logger.LogInformation("Kategori güncellendi: {PublicId}", publicId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        existingCategory.PublicId,
                        existingCategory.Name,
                        existingCategory.Description,
                        existingCategory.RowVersion
                    },
                    message = "Kategori başarıyla güncellendi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori güncellenirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategori sil (soft delete - Admin)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{publicId:guid}")]
        public async Task<IActionResult> DeleteCategory(Guid publicId)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var query = _unitOfWork.Categories.GetAll();
                var category = await query.FirstOrDefaultAsync(c => c.PublicId == publicId);

                if (category == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                // Kategoriye ait kitap var mı kontrol et
                var booksInCategory = await _unitOfWork.Books.GetBooksByCategoryAsync(category.Id);
                if (booksInCategory.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Bu kategoriye ait kitaplar var. Önce kitapları silin veya başka kategoriye taşıyın."
                    });
                }

                //  SoftDeleteAsync metodunu kullan
                await _unitOfWork.Categories.SoftDeleteAsync(category, currentUserId);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Kategori silindi: {PublicId}, DeletedBy={DeletedBy}",
                    publicId, currentUserId);

                return Ok(new { success = true, message = "Kategori başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori silinirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategorideki kitap sayısı
        /// </summary>
        [HttpGet("{publicId:guid}/book-count")]
        public async Task<IActionResult> GetBookCount(Guid publicId)
        {
            try
            {
                var query = _unitOfWork.Categories.GetAll();
                var category = await query.FirstOrDefaultAsync(c => c.PublicId == publicId);

                if (category == null)
                    return NotFound(new { success = false, message = "Kategori bulunamadı" });

                var books = await _unitOfWork.Books.GetBooksByCategoryAsync(category.Id);
                var count = books.Count();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        categoryPublicId = publicId,
                        categoryName = category.Name,
                        bookCount = count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori kitap sayısı getirilirken hata oluştu: {PublicId}", publicId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kategori arama (Pagination ile)
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchCategories(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new { success = false, message = "Arama kelimesi gerekli" });

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var searchQuery = _unitOfWork.Categories.GetAll()
                    .Where(c => c.Name.Contains(query) ||
                               (c.Description != null && c.Description.Contains(query)));

                var totalCount = await searchQuery.CountAsync();

                var categories = await searchQuery
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.PublicId,
                        c.Name,
                        c.Description,
                        c.IsActive
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = categories,
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
                _logger.LogError(ex, "Kategori arama yapılırken hata oluştu: {Query}", query);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}