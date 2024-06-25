using Microsoft.EntityFrameworkCore;
using OneSourceTaskScheduler.Data;
using System.Linq.Expressions;

namespace OneSourceTaskScheduler.Repositories
{
    public class OneSourceRepository : IOneSourceRepository
    {
        private readonly OneSourceContext _context;

        public OneSourceRepository(OneSourceContext context)
        {
            _context = context;
        }

        public async Task<int> CountAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            return  await _context.Set<TEntity>().CountAsync(predicate);
        }

        public async Task RemoveAsync<TEntity>(IReadOnlyList<TEntity> entities) where TEntity : class
        {
            _context.Set<TEntity>().RemoveRange(entities);

            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Set<TEntity>().Remove(entity);

            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            return await _context.Set<TEntity>().Where(predicate).ToListAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAsync<TEntity, TProperty>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TProperty>> include) where TEntity : class
        {
            return await _context.Set<TEntity>().Where(predicate).Include(include).ToListAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAllAsync<TEntity>() where TEntity : class
        {
            return await _context.Set<TEntity>().ToListAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAllAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> include) where TEntity : class
        {
            return await _context.Set<TEntity>().Include(include).ToListAsync();
        }

        public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class
        {
            return _context.Set<TEntity>().AsQueryable();
        }

        public async Task<IReadOnlyList<TEntity>> GetAllOrderedAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> orderBy) where TEntity : class
        {
            var query = _context.Set<TEntity>().OrderBy(orderBy);
            
            return await query.ToListAsync();
        }

        public async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Set<TEntity>().Add(entity);

            await _context.SaveChangesAsync();
        }

        public async Task<int> AddManyAsync<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            _context.Set<TEntity>().AddRange(entities);

            return await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetOrderedAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> orderBy, Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            var query = _context.Set<TEntity>().Where(predicate).OrderBy(orderBy);

            return await query.ToListAsync();
        }

        public async Task<TEntity> GetOneAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            var entity = await _context.Set<TEntity>().FirstOrDefaultAsync(predicate);

            return entity;
        }

        public async Task<int> UpdateAsync<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Set<TEntity>().Update(entity);

            return await _context.SaveChangesAsync();
        }

        public async Task<int> UpdateRangeAsync<TEntity>(params TEntity[] entities) where TEntity : class
        {
            _context.Set<TEntity>().UpdateRange(entities);

            return await _context.SaveChangesAsync();
        }

        public async Task<int> ExecuteSqlRawAsync(string sql)
        {
            int rowsAffected= await _context.Database.ExecuteSqlRawAsync(sql);

            return rowsAffected;
        }
    }
}
