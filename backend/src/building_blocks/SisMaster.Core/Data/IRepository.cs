using SisMaster.Core.Messages;

namespace SisMaster.Core.Data
{
    public interface IRepository<T> : IAggregateRoot
    {
        IUnitOfWork UnitOfWork { get; }
    }
}
