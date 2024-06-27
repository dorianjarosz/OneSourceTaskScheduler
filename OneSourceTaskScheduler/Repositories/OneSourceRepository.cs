using Microsoft.EntityFrameworkCore;
using OneSourceTaskScheduler.Data;
using System.Linq.Expressions;

namespace OneSourceTaskScheduler.Repositories
{
    public class OneSourceRepository : IOneSourceRepository
    {
        private readonly IDbContextFactory<OneSourceContext> _contextFactory;

        public OneSourceRepository(IDbContextFactory<OneSourceContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<OneSourceContext> CreateDbContextAsync()
        {
            return await _contextFactory.CreateDbContextAsync();
        }

        public async Task MigrateDatabaseAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.Database.MigrateAsync();
        }

        public async Task<int> CountAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return  await context.Set<TEntity>().CountAsync(predicate);
        }

        public async Task RemoveAsync<TEntity>(IReadOnlyList<TEntity> entities) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.Set<TEntity>().RemoveRange(entities);

            await context.SaveChangesAsync();
        }

        public async Task RemoveAsync<TEntity>(TEntity entity) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.Set<TEntity>().Remove(entity);

            await context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<TEntity>().Where(predicate).ToListAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAsync<TEntity, TProperty>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TProperty>> include) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<TEntity>().Where(predicate).Include(include).ToListAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAllAsync<TEntity>() where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<TEntity>().ToListAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetAllAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> include) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<TEntity>().Include(include).ToListAsync();
        }

        public async Task<IQueryable<TEntity>> GetQueryAsync<TEntity>(OneSourceContext dbContext) where TEntity : class
        {
            return dbContext.Set<TEntity>().AsQueryable();
        }

        public async Task<IReadOnlyList<TEntity>> GetAllOrderedAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> orderBy) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Set<TEntity>().OrderBy(orderBy);
            
            return await query.ToListAsync();
        }

        public async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.Set<TEntity>().Add(entity);

            await context.SaveChangesAsync();
        }

        public async Task<int> AddManyAsync<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.Set<TEntity>().AddRange(entities);

            return await context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<TEntity>> GetOrderedAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> orderBy, Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Set<TEntity>().Where(predicate).OrderBy(orderBy);

            return await query.ToListAsync();
        }

        public async Task<TEntity> GetOneAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var entity = await context.Set<TEntity>().FirstOrDefaultAsync(predicate);

            return entity;
        }

        public async Task<int> UpdateAsync<TEntity>(TEntity entity) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.Set<TEntity>().Update(entity);

            return await context.SaveChangesAsync();
        }

        public async Task<int> UpdateRangeAsync<TEntity>(params TEntity[] entities) where TEntity : class
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.Set<TEntity>().UpdateRange(entities);

            return await context.SaveChangesAsync();
        }

        public async Task<int> ExecuteSqlRawAsync(string sql)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            int rowsAffected= await context.Database.ExecuteSqlRawAsync(sql);

            return rowsAffected;
        }
    }
}
