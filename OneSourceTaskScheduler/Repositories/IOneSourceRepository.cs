using System.Linq.Expressions;

namespace OneSourceTaskScheduler.Repositories
{
    public interface IOneSourceRepository
    {

        Task<int> UpdateAsync<TEntity>(TEntity entity) where TEntity : class;

        Task<int> CountAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

        Task<IReadOnlyList<TEntity>> GetAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

        Task<IReadOnlyList<TEntity>> GetAsync<TEntity, TProperty>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TProperty>> include) where TEntity : class;

        Task RemoveAsync<TEntity>(IReadOnlyList<TEntity> entities) where TEntity : class;

        Task RemoveAsync<TEntity>(TEntity entity) where TEntity : class;

        Task<int> UpdateRangeAsync<TEntity>(params TEntity[] entities) where TEntity : class;

        Task<TEntity> GetOneAsync<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

        Task<IReadOnlyList<TEntity>> GetOrderedAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> orderBy,Expression<Func<TEntity, bool>> predicate) where TEntity : class;

        Task<IReadOnlyList<TEntity>> GetAllOrderedAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> orderBy) where TEntity : class;

        Task<IReadOnlyList<TEntity>> GetAllAsync<TEntity>() where TEntity : class;

        Task<IReadOnlyList<TEntity>> GetAllAsync<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> include) where TEntity : class;

        Task AddAsync<TEntity>(TEntity entity) where TEntity : class;

        Task<int> AddManyAsync<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

        IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class;

        Task<int> ExecuteSqlRawAsync(string sql);
    }
}
