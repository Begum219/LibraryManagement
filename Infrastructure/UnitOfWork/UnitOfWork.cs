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
        private IGenericRepository<Loan> _loanRepository;

        public UnitOfWork(LibraryContext context)
        {
            _context = context;
        }

        public IUserRepository Users => _userRepository ??= new UserRepository(_context);

        public IBookRepository Books => _bookRepository ??= new BookRepository(_context);

        public IGenericRepository<Category> Categories => _categoryRepository ??= new GenericRepository<Category>(_context);

        public IGenericRepository<Loan> Loans => _loanRepository ??= new GenericRepository<Loan>(_context);

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