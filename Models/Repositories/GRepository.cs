using DentalLab.Models.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DentalLab.Models.Repositories
{
    public class GRepository<T> : IGRepository<T> where T : class
    {
        private readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public void Insert(T entity) => _dbSet.Add(entity);

        public void Delete(T entity) => _dbSet.Remove(entity);

        public void Update(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public IQueryable<T> GetAll(bool tracking = false)
            => tracking ? _dbSet : _dbSet.AsNoTracking();

        public IQueryable<T> GetWhere(Expression<Func<T, bool>> filter, bool tracking = false)
            => tracking ? _dbSet.Where(filter) : _dbSet.Where(filter).AsNoTracking();

        public async Task<T?> GetByIdAsync(object id)
            => await _dbSet.FindAsync(id);

        public IQueryable<T> Include(params Expression<Func<T, object?>>[] includes)
        {
            IQueryable<T> query = _dbSet;
            foreach (var include in includes)
                query = query.Include(include);

            return query.AsNoTracking();
        }
    }
}
