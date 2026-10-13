using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

public interface IPessoaRepository : IRepository<Pessoa>
{
    Task<Pessoa?> ObterPorCpfAsync(string cpfSomenteDigitos, CancellationToken ct = default);
    Task<Pessoa?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>O cadastro ligado à conta do site (rastreado), se houver.</summary>
    Task<Pessoa?> ObterPorContaAsync(string contaId, CancellationToken ct = default);
    void Adicionar(Pessoa pessoa);
    void Remover(Pessoa pessoa);
}
