using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class PessoaRepository : IPessoaRepository
{
    private readonly CampeonatoDbContext _context;

    public PessoaRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Pessoa?> ObterPorCpfAsync(string cpfSomenteDigitos, CancellationToken ct = default) =>
        await _context.Pessoas.FirstOrDefaultAsync(p => p.Cpf.Numero == cpfSomenteDigitos, ct);

    public void Adicionar(Pessoa pessoa) => _context.Pessoas.Add(pessoa);

    public void Dispose() => _context.Dispose();
}
