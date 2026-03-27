using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DropInBadAPI.Services
{
    public class GenericService<T> : IGenericService<T> where T : class
    {
        protected readonly BadmintonDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericService(BadmintonDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<T?> UpdateAsync(int id, T entity)
        {
            var existingEntity = await _dbSet.FindAsync(id);
            if (existingEntity == null) return null;

            _context.Entry(existingEntity).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
            return existingEntity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity == null) return false;

            // Soft Delete (ถ้าตารางมี Property 'IsActive')
            var isActiveProperty = typeof(T).GetProperty("IsActive");
            if (isActiveProperty != null && isActiveProperty.PropertyType == typeof(bool?))
            {
                isActiveProperty.SetValue(entity, false);
            }
            else
            {
                // Hard Delete (ถ้าไม่มี IsActive)
                _dbSet.Remove(entity);
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}