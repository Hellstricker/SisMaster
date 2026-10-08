using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public interface ICampeonatoRepository : IRepository<Campeonato>
{
    Task<Campeonato?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    void Atualizar(Campeonato campeonato);
    void AdicionarTemporada(Temporada temporada);
}
