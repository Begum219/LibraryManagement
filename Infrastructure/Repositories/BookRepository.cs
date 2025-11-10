using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Domain.Entities;
using LibraryManagement.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class BookRepository : GenericRepository<Book>, IBookRepository
    {
        private readonly LibraryContext _context;

        public BookRepository(LibraryContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Book>> GetBooksByCategoryAsync(int categoryId)
        {
            return await _context.Books
                .Where(b => b.CategoryId == categoryId && b.IsActive == true)
                .Include(b => b.Category)
                .ToListAsync();
        }

        public async Task<Book> GetBookWithDetailsAsync(int id)
        {
            return await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Loans)
                .ThenInclude(l => l.User)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<bool> IsBookAvailableAsync(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            return book != null && book.AvailableCopies > 0 && book.IsActive == true;
        }
       

        public async Task<Book?> GetByPublicIdAsync(Guid publicId)
        {
            return await _context.Books
                .FirstOrDefaultAsync(b => b.PublicId == publicId);
        }

        public async Task<Book?> GetBookWithDetailsByPublicIdAsync(Guid publicId)
        {
            return await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Loans)
                .FirstOrDefaultAsync(b => b.PublicId == publicId);
        }
    }
}