using Domain.Entities;
using LibraryManagement.Application.Interfaces.Repositories;

namespace LibraryManagement.Application.Interfaces.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IBookRepository Books { get; }
        IGenericRepository<Category> Categories { get; }
        IGenericRepository<Loan> Loans { get; }
        Task<int> SaveChangesAsync();
    }
}