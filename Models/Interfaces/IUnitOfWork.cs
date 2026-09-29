namespace DentalLab.Models.Interfaces
{
    public interface IUnitOfWork<T>
    {
        IGRepository<T> Repository { get; }
        Task SaveAsync();
    }
}
