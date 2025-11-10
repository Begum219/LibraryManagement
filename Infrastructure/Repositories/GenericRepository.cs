using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Domain.Entities;
using LibraryManagement.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class, IEntity  // ← IEntity ekle
    {
        protected readonly LibraryContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(LibraryContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<T> GetByIdAsync(int id)
        {
            // ✅ Soft delete filtresi ekle
            var entity = await _dbSet.FindAsync(id);
            if (entity != null && entity.IsDeleted)
                return null;
            return entity;
        }

        public async Task<T?> GetByPublicIdAsync(Guid publicId)
        {
            // ✅ PublicId ile getir + soft delete filtresi
            return await _dbSet
                .Where(e => e.PublicId == publicId && e.IsDeleted == false)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            // ✅ Sadece silinmemiş kayıtlar
            return await _dbSet
                .Where(e => e.IsDeleted == false)
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
        {
            // ✅ Soft delete filtresi ekle
            return await _dbSet
                .Where(expression)
                .Where(e => e.IsDeleted == false)
                .ToListAsync();
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        // ✅ SOFT DELETE METODLARI
        public virtual async Task SoftDeleteAsync(T entity, int deletedBy)
        {
            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.DeletedDate = DateTime.UtcNow;
            entity.DeletedBy = deletedBy;
            entity.UpdatedDate = DateTime.UtcNow;

            _dbSet.Update(entity);
        }

        public virtual async Task RestoreAsync(T entity)
        {
            entity.IsDeleted = false;
            entity.IsActive = true;
            entity.DeletedDate = null;
            entity.DeletedBy = null;
            entity.UpdatedDate = DateTime.UtcNow;

            _dbSet.Update(entity);
        }

        public virtual async Task<IEnumerable<T>> GetDeletedAsync()
        {
            return await _dbSet
                .Where(e => e.IsDeleted == true)
                .ToListAsync();
        }
    }
}