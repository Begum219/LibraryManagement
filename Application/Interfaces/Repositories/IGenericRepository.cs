using System.Linq.Expressions;

namespace LibraryManagement.Application.Interfaces.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression);
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
       
        // ✅ Soft Delete metodları
        Task SoftDeleteAsync(T entity, int deletedBy);
        Task RestoreAsync(T entity);
        Task<IEnumerable<T>> GetDeletedAsync();
    }
}