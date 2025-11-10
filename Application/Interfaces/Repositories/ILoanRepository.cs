using Domain.Entities;

namespace LibraryManagement.Application.Interfaces.Repositories
{
    public interface ILoanRepository : IGenericRepository<Loan>
    {
        Task<Loan?> GetLoanWithDetailsAsync(int id);
        Task<Loan?> GetByPublicIdAsync(Guid publicId);  // ← BUNU EKLE
        Task<Loan?> GetLoanWithDetailsByPublicIdAsync(Guid publicId);  // ← BUNU DA EKLE
        Task<IEnumerable<Loan>> GetUserLoansAsync(int userId);
        Task<IEnumerable<Loan>> GetActiveLoansByUserAsync(int userId);
        Task<IEnumerable<Loan>> GetOverdueLoansAsync();

    }
}