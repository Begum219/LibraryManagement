using Domain.Entities;
using LibraryManagement.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class LoanRepository : GenericRepository<Loan>, ILoanRepository
    {
        public LoanRepository(LibraryContext context) : base(context) { }

        public async Task<Loan?> GetLoanWithDetailsAsync(int id)
        {
            return await _context.Loans
                .Include(l => l.User)
                .Include(l => l.Book)
                    .ThenInclude(b => b.Category)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<Loan?> GetByPublicIdAsync(Guid publicId)
        {
            return await _context.Loans
                .FirstOrDefaultAsync(l => l.PublicId == publicId);
        }

        public async Task<Loan?> GetLoanWithDetailsByPublicIdAsync(Guid publicId)
        {
            return await _context.Loans
                .Include(l => l.User)
                .Include(l => l.Book)
                    .ThenInclude(b => b.Category)
                .FirstOrDefaultAsync(l => l.PublicId == publicId);
        }

        public async Task<IEnumerable<Loan>> GetUserLoansAsync(int userId)
        {
            return await _context.Loans
                .Include(l => l.Book)
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.LoanDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Loan>> GetActiveLoansByUserAsync(int userId)
        {
            return await _context.Loans
                .Include(l => l.Book)
                 .Where(l => l.UserId == userId && l.IsReturned == false)
                .ToListAsync();
        }

       

        // Gecikmiş ve henüz iade edilmemiş kitapları getir
        public async Task<IEnumerable<Loan>> GetOverdueLoansAsync()
        {
            return await _context.Loans
                .Include(l => l.User)
                .Include(l => l.Book)
                .Where(l => l.IsActive == true && l.IsReturned == false  && l.DueDate < DateTime.Now)
                    
                    
                .ToListAsync();
        }

        // Yaklaşan iade tarihi olan kitapları getir (opsiyonel - erken uyarı için)
        public async Task<IEnumerable<Loan>> GetLoansWithUpcomingDueDateAsync(int daysBeforeDue)
        {
            var targetDate = DateTime.Now.AddDays(daysBeforeDue);

            return await _context.Loans
                .Include(l => l.User)
                .Include(l => l.Book)
                .Where(l => l.IsActive == true
                    && l.IsReturned == false
                    && l.DueDate <= targetDate
                    && l.DueDate > DateTime.Now)
                .ToListAsync();
        }
    }
}