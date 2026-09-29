using DentalLab.Models.Interfaces;
using DentalLab.Models.Repositories;

namespace DentalLab.Models.UnitOfWork
{
    public class UnitOfWork<T> : IUnitOfWork<T>, IDisposable where T : class
    {
        private readonly AppDbContext _context;
        private IGRepository<T>? _entity;
        private bool _disposed;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IGRepository<T> Repository => _entity ??= new GRepository<T>(_context);

        public async Task SaveAsync() => await _context.SaveChangesAsync();

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    _context.Dispose();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
