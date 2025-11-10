using Domain.Entities;


namespace LibraryManagement.Application.Interfaces.Repositories
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User> GetByEmailAsync(string email);
        Task<bool> UserExistsAsync(string email);
        Task<User?> GetByPublicIdAsync(Guid publicId);
    }
}