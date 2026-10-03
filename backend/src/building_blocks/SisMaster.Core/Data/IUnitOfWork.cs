namespace SisMaster.Core.Data
{
    public interface IUnitOfWork
    {
        Task<bool> Commit();
    }
}
