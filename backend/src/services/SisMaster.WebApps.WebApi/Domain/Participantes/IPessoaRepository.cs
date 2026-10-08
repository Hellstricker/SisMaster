using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

public interface IPessoaRepository : IRepository<Pessoa>
{
    Task<Pessoa?> ObterPorCpfAsync(string cpfSomenteDigitos, CancellationToken ct = default);
    void Adicionar(Pessoa pessoa);
}
