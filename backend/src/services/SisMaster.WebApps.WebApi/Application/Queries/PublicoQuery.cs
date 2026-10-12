using Microsoft.EntityFrameworkCore;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Leitura para o site público: só o que qualquer visitante pode ver (nada de pessoa, CPF, e-mail ou valor de cobrança).
/// A associação é achada pela sigla; associação inativa não aparece.
/// </summary>
public class PublicoQuery
{
    private readonly CampeonatoDbContext _context;

    public PublicoQuery(CampeonatoDbContext context) => _context = context;

    public async Task<object?> AssociacaoAsync(string sigla, CancellationToken ct)
    {
        var a = await _context.Associacoes.AsNoTracking()
            .Where(x => x.Ativa && x.Sigla == sigla)
            .Select(x => new { x.Id, x.Nome, x.Sigla, x.Uf })
            .FirstOrDefaultAsync(ct);
        return a;
    }

    /// <summary>"A associação" e "Contato": só o que a associação preencheu (cargo sem nome não aparece).</summary>
    public async Task<object?> PaginaAsync(string sigla, CancellationToken ct)
    {
        var a = await _context.Associacoes.AsNoTracking().FirstOrDefaultAsync(x => x.Ativa && x.Sigla == sigla, ct);
        if (a is null) return null;
        var p = await _context.PaginasPublicas.AsNoTracking().Include(x => x.Diretoria).FirstOrDefaultAsync(x => x.AssociacaoId == a.Id, ct);

        return new
        {
            Associacao = new { a.Nome, a.Sigla, a.Uf },
            Missao = p?.Missao,
            QuemSomos = p?.QuemSomos,
            Gestao = p?.GestaoDaDiretoria,
            Diretoria = (p?.Diretoria ?? []).Where(m => m.Nome != null).OrderBy(m => m.Ordem).Select(m => new { m.Cargo, m.Nome }).ToList(),
            Contato = new { p?.Whatsapp, p?.Instagram, p?.Email, p?.OndeJogamos },
            // Dados da controladora para a política de privacidade e os termos (são públicos por natureza).
            Legal = new { p?.Cnpj, p?.Endereco, p?.EncarregadoDeDados, p?.EmailPrivacidade, p?.ForoCidade }
        };
    }

    /// <summary>
    /// Temporadas com inscrições abertas hoje: situação "inscrições abertas", habilitadas e dentro da janela de datas
    /// (o último dia da janela ainda vale).
    /// </summary>
    public async Task<object?> InscricoesAbertasAsync(string sigla, DateTime agora, CancellationToken ct)
    {
        var associacao = await _context.Associacoes.AsNoTracking().FirstOrDefaultAsync(x => x.Ativa && x.Sigla == sigla, ct);
        if (associacao is null) return null;

        var hoje = agora.Date;
        var temporadas = await _context.Temporadas.AsNoTracking()
            .Include(t => t.Categorias)
            .Include(t => t.Campeonato)
            .Where(t => t.Campeonato.AssociacaoId == associacao.Id && t.Campeonato.Ativo
                && t.Status == StatusTemporada.InscricoesAbertas && t.InscricoesHabilitadas
                && t.DataInicioInscricoes.Date <= hoje && t.DataFimInscricoes.Date >= hoje)
            .OrderBy(t => t.DataFimInscricoes)
            .ToListAsync(ct);

        return new
        {
            Temporadas = temporadas.Select(t => new
            {
                TemporadaId = t.Id,
                Campeonato = t.Campeonato.Nome,
                t.Ano,
                InicioInscricoes = t.DataInicioInscricoes,
                FimInscricoes = t.DataFimInscricoes,
                DiasRestantes = (t.DataFimInscricoes.Date - hoje).Days,
                Categorias = t.Categorias.OrderBy(c => c.Nome)
                    .Select(c => new { c.Id, c.Nome, c.IdadeMinima, Sexo = c.Sexo == null ? null : c.Sexo.ToString() }).ToList()
            }).ToList()
        };
    }
}
