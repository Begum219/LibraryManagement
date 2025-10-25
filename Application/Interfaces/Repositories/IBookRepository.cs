using Domain.Entities;


namespace LibraryManagement.Application.Interfaces.Repositories
{
    public interface IBookRepository : IGenericRepository<Book>
    {
        Task<IEnumerable<Book>> GetBooksByCategoryAsync(int categoryId);
        Task<Book> GetBookWithDetailsAsync(int id);
        Task<bool> IsBookAvailableAsync(int bookId);
    }
}