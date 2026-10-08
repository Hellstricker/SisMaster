using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class SumulaRepository : ISumulaRepository
{
    private readonly CampeonatoDbContext _context;

    public SumulaRepository(CampeonatoDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    private IQueryable<Sumula> ComTudo() =>
        _context.Sumulas
            .Include(s => s.Eventos)
            .Include(s => s.Substituicoes)
            .Include(s => s.Times).ThenInclude(t => t.Jogadores);

    public async Task<Sumula?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await ComTudo().AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Sumula?> ObterPorJogoAsync(Guid jogoId, CancellationToken ct = default) =>
        await ComTudo().AsSplitQuery().FirstOrDefaultAsync(s => s.JogoId == jogoId, ct);

    public async Task<bool> ExisteParaJogoAsync(Guid jogoId, CancellationToken ct = default) =>
        await _context.Sumulas.AnyAsync(s => s.JogoId == jogoId, ct);

    public void Adicionar(Sumula sumula) => _context.Sumulas.Add(sumula);
    public void Remover(Sumula sumula) => _context.Sumulas.Remove(sumula);
    public void AdicionarSubstituicao(Substituicao substituicao) => _context.SubstituicoesSumula.Add(substituicao);
    public void RemoverSubstituicao(Substituicao substituicao) => _context.SubstituicoesSumula.Remove(substituicao);

    public async Task<IReadOnlyList<Sumula>> ListarPorJogosAsync(IReadOnlyCollection<Guid> jogoIds, CancellationToken ct = default) =>
        await _context.Sumulas
            .Include(s => s.Substituicoes)
            .Include(s => s.Times).ThenInclude(t => t.Jogadores)
            .AsSplitQuery()
            .Where(s => jogoIds.Contains(s.JogoId)).ToListAsync(ct);

    public async Task<bool> ExisteOutraComCodigoExternoAsync(string codigoExterno, Guid sumulaId, CancellationToken ct = default) =>
        await _context.Sumulas.AnyAsync(s => s.CodigoExterno == codigoExterno && s.Id != sumulaId, ct);

    public void AplicarImportacao(ResultadoImportacao resultado)
    {
        _context.EventosSumula.RemoveRange(resultado.EventosRemovidos);
        _context.SubstituicoesSumula.RemoveRange(resultado.SubstituicoesRemovidas);
        _context.EventosSumula.AddRange(resultado.EventosNovos);
        _context.SubstituicoesSumula.AddRange(resultado.SubstituicoesNovas);
    }

    public async Task GuardarDadosExternosAsync(Guid sumulaId, string json, CancellationToken ct = default)
    {
        var existente = await _context.SumulaDadosExternos.FirstOrDefaultAsync(d => d.SumulaId == sumulaId, ct);
        if (existente is null) _context.SumulaDadosExternos.Add(new SumulaDadosExternos(sumulaId, json)); // Add explícito, nunca por detecção
        else existente.Substituir(json);
    }

    public async Task<string?> ObterDadosExternosAsync(Guid sumulaId, CancellationToken ct = default) =>
        await _context.SumulaDadosExternos.Where(d => d.SumulaId == sumulaId).Select(d => d.Json).FirstOrDefaultAsync(ct);

    public void Atualizar(Sumula sumula) { }
    public void AdicionarEvento(EventoSumula evento) => _context.EventosSumula.Add(evento);
    public void AdicionarJogadores(IEnumerable<Jogador> jogadores) => _context.SumulaJogadores.AddRange(jogadores);
    public void RemoverJogadores(IEnumerable<Jogador> jogadores) => _context.SumulaJogadores.RemoveRange(jogadores);

    public void Dispose() => _context.Dispose();
}
