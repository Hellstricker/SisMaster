using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Time;

public interface ITimeRepository : IRepository<Time>
{
    Task<Time?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Time>> ListarAsync(CancellationToken ct = default);
    Task<Jogador?> ObterJogadorPorIdAsync(Guid id, CancellationToken ct = default);
    void Adicionar(Time time);
    void Atualizar(Time time);
}
