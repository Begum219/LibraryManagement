using System;
using System.Threading.Tasks;
using LibraryManagement.Application.Interfaces.Repositories;
using LibraryManagement.Application.Interfaces;
using Domain.Entities;
using Infrastructure;
using Infrastructure.Repositories;
using Domain;
using LibraryManagement.Application.Interfaces.UnitOfWork;

namespace Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly LibraryContext _context;
        private IUserRepository _userRepository;
        private IBookRepository _bookRepository;
        private IGenericRepository<Category> _categoryRepository;
        private ILoanRepository _loanRepository;  // ← ILoanRepository yap

        public UnitOfWork(LibraryContext context)
        {
            _context = context;
        }

        public IUserRepository Users => _userRepository ??= new UserRepository(_context);

        public IBookRepository Books => _bookRepository ??= new BookRepository(_context);

        public IGenericRepository<Category> Categories => _categoryRepository ??= new GenericRepository<Category>(_context);

        public ILoanRepository Loans => _loanRepository ??= new LoanRepository(_context);  // ← ILoanRepository ve LoanRepository yap

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}