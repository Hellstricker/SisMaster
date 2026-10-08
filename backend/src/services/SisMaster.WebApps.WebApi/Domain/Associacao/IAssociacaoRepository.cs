using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public interface IAssociacaoRepository : IRepository<Associacao>
{
    Task<Associacao?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Associacao>> ListarAsync(CancellationToken ct = default);
    void Adicionar(Associacao associacao);
    void Atualizar(Associacao associacao);
    void AdicionarCampeonato(Campeonato campeonato);
}
