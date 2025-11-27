using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Domain.Entities;
using LibraryManagement.Application.DTOs.Common;
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
            // Soft delete filtresi ekle
            var entity = await _dbSet.FindAsync(id);
            if (entity != null && entity.IsDeleted)
                return null;
            return entity;
        }

        public async Task<T?> GetByPublicIdAsync(Guid publicId)
        {
            // PublicId ile getir + soft delete filtresi
            return await _dbSet
                .Where(e => e.PublicId == publicId && e.IsDeleted == false)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            // Sadece silinmemiş kayıtlar
            return await _dbSet
                .Where(e => e.IsDeleted == false)
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
        {
            // Soft delete filtresi ekle
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
        //  GetAll (IQueryable)
        public IQueryable<T> GetAll()
        {
            return _context.Set<T>().AsQueryable();
        }
        public async Task<PaginationResponse<T>> GetPagedAsync(
    int page,
    int pageSize,
    Expression<Func<T, bool>>? filter = null,
    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            IQueryable<T> query = _context.Set<T>();

            // Soft delete filtresi
            if (typeof(T).GetProperty("IsDeleted") != null)
            {
                var parameter = Expression.Parameter(typeof(T), "e");
                var property = Expression.Property(parameter, "IsDeleted");
                var constant = Expression.Constant(false);
                var equal = Expression.Equal(property, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(equal, parameter);
                query = query.Where(lambda);
            }

            // Ek filtre
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // Toplam kayıt sayısı
            var totalCount = await query.CountAsync();

            // Sıralama
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            // Sayfalama
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginationResponse<T>
            {
                Data = data,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
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

        // SOFT DELETE METODLARI
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