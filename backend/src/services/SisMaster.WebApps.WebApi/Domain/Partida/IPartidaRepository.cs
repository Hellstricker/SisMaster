using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Partida;

public interface IPartidaRepository : IRepository<Partida>
{
    Task<Partida?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Partida>> ListarAsync(CancellationToken ct = default);
    void Adicionar(Partida partida);
    void Atualizar(Partida partida);
    void AdicionarEvento(EventoPartida evento);
}
