using Microsoft.EntityFrameworkCore;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Vitrine pública (só leitura): jogos de hoje e ao vivo, resultados, campeonatos, tabela e classificação da temporada e a súmula
/// do jogo. Nunca devolve CPF, e-mail, telefone, nascimento ou situação de inscrição; os únicos dados de pessoa são o nome e a
/// camisa do atleta dentro da súmula do jogo (como o placar público já faz). Só enxerga associações ativas.
/// </summary>
public class PublicoVitrineQuery
{
    private readonly CampeonatoDbContext _context;
    private readonly IFaseRepository _faseRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly ClassificacaoFaseQuery _classificacao;

    public PublicoVitrineQuery(CampeonatoDbContext context, IFaseRepository faseRepository, IJogoRepository jogoRepository,
        IEquipeRepository equipeRepository, ISumulaRepository sumulaRepository, ClassificacaoFaseQuery classificacao)
    {
        _context = context;
        _faseRepository = faseRepository;
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
        _sumulaRepository = sumulaRepository;
        _classificacao = classificacao;
    }

    private Task<Associacao?> AssociacaoAtivaAsync(string sigla, CancellationToken ct) =>
        _context.Associacoes.AsNoTracking().FirstOrDefaultAsync(a => a.Ativa && a.Sigla == sigla, ct);

    /// <summary>Temporada de uma associação ativa e de campeonato ativo; nula se não existir ou não for pública.</summary>
    private Task<Temporada?> TemporadaPublicaAsync(Guid temporadaId, CancellationToken ct) =>
        _context.Temporadas.AsNoTracking().Include(t => t.Campeonato).Include(t => t.Categorias)
            .FirstOrDefaultAsync(t => t.Id == temporadaId && t.Campeonato.Ativo
                && _context.Associacoes.Any(a => a.Id == t.Campeonato.AssociacaoId && a.Ativa), ct);

    // ------------------------------------------------------------------ início

    public async Task<object?> InicioAsync(string sigla, DateOnly hoje, CancellationToken ct)
    {
        var associacao = await AssociacaoAtivaAsync(sigla, ct);
        if (associacao is null) return null;

        var temporadas = await _context.Temporadas.AsNoTracking()
            .Where(t => t.Campeonato.AssociacaoId == associacao.Id && t.Campeonato.Ativo && t.Status != StatusTemporada.InscricoesAbertas)
            .Select(t => t.Id).ToListAsync(ct);

        var jogos = await _context.Jogos.AsNoTracking()
            .Include(j => j.Casa).Include(j => j.Visitante).Include(j => j.Local)
            .Where(j => temporadas.Contains(j.TemporadaId) && j.Data != null && j.Status != StatusJogo.Dispensado)
            .ToListAsync(ct);

        var aoVivo = jogos.Where(j => j.Status == StatusJogo.EmAndamento).OrderBy(j => j.Hora).ToList();
        var deHoje = jogos.Where(j => j.Status == StatusJogo.Agendado && j.Data == hoje).OrderBy(j => j.Hora).ThenBy(j => j.Numero).ToList();
        var proximos = jogos.Where(j => j.Status == StatusJogo.Agendado && j.Data > hoje)
            .OrderBy(j => j.Data).ThenBy(j => j.Hora).ThenBy(j => j.Numero).Take(10).ToList();
        var resultados = jogos.Where(j => j.Status is StatusJogo.Encerrado or StatusJogo.WO)
            .OrderByDescending(j => j.Data).ThenByDescending(j => j.Hora).ThenByDescending(j => j.Numero).Take(8).ToList();

        var todos = aoVivo.Concat(deHoje).Concat(proximos).Concat(resultados).DistinctBy(j => j.Id).ToList();
        var cartoes = (await CartoesAsync(todos, ct)).ToDictionary(c => c.Id);
        object[] Lista(IEnumerable<Jogo> l) => l.Select(j => (object)cartoes[j.Id]).ToArray();

        return new
        {
            Hoje = hoje.ToString("yyyy-MM-dd"),
            AoVivo = Lista(aoVivo),
            JogosDeHoje = Lista(deHoje),
            Proximos = Lista(proximos),
            Resultados = Lista(resultados)
        };
    }

    // ------------------------------------------------------------------ campeonatos e temporada

    public async Task<object?> CampeonatosAsync(string sigla, CancellationToken ct)
    {
        var associacao = await AssociacaoAtivaAsync(sigla, ct);
        if (associacao is null) return null;

        var temporadas = await _context.Temporadas.AsNoTracking().Include(t => t.Campeonato)
            .Where(t => t.Campeonato.AssociacaoId == associacao.Id && t.Campeonato.Ativo)
            .OrderByDescending(t => t.Ano).ThenBy(t => t.Campeonato.Nome).ToListAsync(ct);

        return new
        {
            Temporadas = temporadas.Select(t => new { t.Id, Campeonato = t.Campeonato.Nome, t.Ano, Status = t.Status.ToString() }).ToList()
        };
    }

    public async Task<object?> TemporadaAsync(Guid temporadaId, CancellationToken ct)
    {
        var temporada = await TemporadaPublicaAsync(temporadaId, ct);
        if (temporada is null) return null;

        var fases = await _faseRepository.ListarPorTemporadaAsync(temporadaId, ct);
        var equipes = await _equipeRepository.ListarPorTemporadaAsync(temporadaId, ct);
        var jogos = temporada.TabelaJogosGerada ? await _jogoRepository.ListarPorTemporadaAsync(temporadaId, ct) : [];
        var cartoes = await CartoesAsync(jogos.Where(j => j.Status != StatusJogo.Dispensado).ToList(), ct);

        var atletasPorEquipe = await _context.Atletas.AsNoTracking()
            .Where(a => _context.Equipes.Any(e => e.Id == a.EquipeId && e.TemporadaCategoria.TemporadaId == temporadaId))
            .GroupBy(a => a.EquipeId).Select(g => new { EquipeId = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.EquipeId, x => x.Total, ct);
        var categorias = temporada.Categorias.ToDictionary(c => c.Id);

        return new
        {
            temporada.Id,
            Campeonato = temporada.Campeonato.Nome,
            Publico = temporada.Publico.ToString(),
            temporada.Ano,
            Status = temporada.Status.ToString(),
            TabelaGerada = temporada.TabelaJogosGerada,
            TotalEquipes = equipes.Count,
            Categorias = temporada.Categorias.OrderBy(c => c.Nome).Select(c => new { c.Id, c.Nome }).ToList(),
            Fases = fases.OrderBy(f => f.Ordem).Select(f => new { f.Id, f.Nome, f.Ordem, Tipo = f.Tipo.ToString(), CategoriaId = f.TemporadaCategoriaId }).ToList(),
            Equipes = equipes.OrderBy(e => e.Nome).Select(e => new
            {
                e.Id, e.Nome, e.Cor, CategoriaId = e.TemporadaCategoriaId, Categoria = categorias[e.TemporadaCategoriaId].Nome,
                Atletas = atletasPorEquipe.GetValueOrDefault(e.Id)
            }).ToList(),
            Jogos = cartoes.OrderBy(c => c.Numero).ToList()
        };
    }

    public async Task<object?> ClassificacaoAsync(Guid faseId, CancellationToken ct)
    {
        var fase = await _faseRepository.ObterPorIdAsync(faseId, ct);
        if (fase is null) return null;
        var visivel = await _context.TemporadaCategorias.AsNoTracking().AnyAsync(c => c.Id == fase.TemporadaCategoriaId
            && _context.Temporadas.Any(t => t.Id == c.TemporadaId && t.Campeonato.Ativo
                && _context.Associacoes.Any(a => a.Id == t.Campeonato.AssociacaoId && a.Ativa)), ct);
        return visivel ? await _classificacao.ExecutarAsync(faseId, ct) : null;
    }

    // ------------------------------------------------------------------ jogo

    public async Task<object?> JogoAsync(Guid jogoId, CancellationToken ct)
    {
        var jogo = await _context.Jogos.AsNoTracking().Include(j => j.Casa).Include(j => j.Visitante).Include(j => j.Local)
            .FirstOrDefaultAsync(j => j.Id == jogoId, ct);
        if (jogo is null || jogo.Status == StatusJogo.Dispensado) return null;
        var temporada = await TemporadaPublicaAsync(jogo.TemporadaId, ct);
        if (temporada is null) return null;

        var cartao = (await CartoesAsync([jogo], ct)).Single();
        object? sumulaDto = null;
        if (jogo.SumulaId is not null)
        {
            var s = await _sumulaRepository.ObterPorJogoAsync(jogo.Id, ct);
            if (s is not null && s.Status != StatusSumula.EmPreparacao) sumulaDto = MontarSumula(s);
        }

        return new
        {
            Jogo = cartao,
            Campeonato = new { temporada.Id, Nome = temporada.Campeonato.Nome, temporada.Ano },
            Sumula = sumulaDto
        };
    }

    private static object MontarSumula(Sumula s)
    {
        var casa = s.TimeDoLado(LadoTime.Casa);
        var visitante = s.TimeDoLado(LadoTime.Visitante);
        var porJogador = s.Eventos.GroupBy(e => e.JogadorId).ToDictionary(g => g.Key, g => g.ToList());

        object Lado(Time t) => new
        {
            t.Nome,
            t.Tecnico,
            Jogadores = t.Jogadores
                .OrderBy(j => int.TryParse(j.Numero, out var n) ? n : 0).ThenBy(j => j.Numero)
                .Select(j =>
                {
                    var ev = porJogador.GetValueOrDefault(j.Id) ?? [];
                    return new
                    {
                        j.Numero,
                        j.Nome,
                        Pontos = ev.Sum(e => e.PontosGerados),
                        Rebotes = ev.Count(e => e.Tipo is TipoEvento.ReboteOfensivo or TipoEvento.ReboteDefensivo),
                        Assistencias = ev.Count(e => e.Tipo == TipoEvento.Assistencia),
                        Roubadas = ev.Count(e => e.Tipo == TipoEvento.Roubada),
                        Tocos = ev.Count(e => e.Tipo == TipoEvento.Toco),
                        Faltas = ev.Count(e => e.Tipo is TipoEvento.FaltaPessoal or TipoEvento.FaltaTecnica or TipoEvento.FaltaAntiDesportiva)
                    };
                }).ToList()
        };

        int PontosNoPeriodo(Time t, Periodo p)
        {
            var ids = t.Jogadores.Select(j => j.Id).ToHashSet();
            return s.Eventos.Where(e => e.Periodo == p && ids.Contains(e.JogadorId)).Sum(e => e.PontosGerados);
        }

        var periodos = Enum.GetValues<Periodo>().Where(p => (int)p <= (int)s.PeriodoAtual).ToList();
        return new
        {
            Status = s.Status.ToString(),
            Periodo = (int)s.PeriodoAtual,
            s.PlacarCasa,
            s.PlacarVisitante,
            Parciais = periodos.Select(p => new { Periodo = (int)p, Casa = PontosNoPeriodo(casa, p), Visitante = PontosNoPeriodo(visitante, p) }).ToList(),
            Casa = Lado(casa),
            Visitante = Lado(visitante)
        };
    }

    // ------------------------------------------------------------------ cartão de jogo (compartilhado)

    /// <summary>Resumo público de cada jogo: equipes (ou a origem, enquanto indefinidas), data, local, placar e, se ao vivo, o estado da súmula.</summary>
    private async Task<List<CartaoDeJogo>> CartoesAsync(IReadOnlyList<Jogo> jogos, CancellationToken ct)
    {
        var resultado = new List<CartaoDeJogo>();
        if (jogos.Count == 0) return resultado;

        var aoVivoIds = jogos.Where(j => j.Status == StatusJogo.EmAndamento).Select(j => j.Id).ToList();
        var vivas = aoVivoIds.Count == 0 ? new Dictionary<Guid, Sumula>() : (await _sumulaRepository.ListarPorJogosAsync(aoVivoIds, ct)).ToDictionary(s => s.JogoId);

        foreach (var grupo in jogos.GroupBy(j => j.TemporadaId))
        {
            var temporada = await _context.Temporadas.AsNoTracking().Include(t => t.Campeonato).Include(t => t.Categorias).FirstAsync(t => t.Id == grupo.Key, ct);
            var fases = await _faseRepository.ListarPorTemporadaAsync(temporada.Id, ct);
            var confrontos = await _jogoRepository.ListarConfrontosDaTemporadaAsync(temporada.Id, ct);
            var equipes = await _equipeRepository.ListarPorTemporadaAsync(temporada.Id, ct);
            var texto = new TextoDeReferencia(equipes, fases, confrontos);
            var faseDe = fases.ToDictionary(f => f.Id);
            var categorias = temporada.Categorias.ToDictionary(c => c.Id);
            var confrontoDe = confrontos.ToDictionary(c => c.Id);

            LadoDoJogo Lado(FaseEquipe v)
            {
                var e = texto.Equipe(v.EquipeId);
                return new LadoDoJogo(e?.Nome ?? texto.Descrever(v.Origem), e is not null);
            }

            foreach (var j in grupo)
            {
                var fase = faseDe[j.FaseId];
                var confronto = j.ConfrontoId is null ? null : confrontoDe.GetValueOrDefault(j.ConfrontoId.Value);
                AoVivo? vivo = null;
                if (j.Status == StatusJogo.EmAndamento && vivas.TryGetValue(j.Id, out var s) && s.Status != StatusSumula.EmPreparacao)
                    vivo = new AoVivo(s.Id, (int)s.PeriodoAtual, s.PlacarCasa, s.PlacarVisitante, s.CronometroSegundosRestantesAgora, s.CronometroAtivo);

                resultado.Add(new CartaoDeJogo(
                    j.Id, j.Numero, j.Data?.ToString("yyyy-MM-dd"), j.Hora?.ToString("HH:mm"),
                    j.Local is null ? null : new LocalDoJogo(j.Local.Nome, j.Local.Cidade, j.Local.Estado),
                    categorias[fase.TemporadaCategoriaId].Nome, fase.Nome,
                    confronto is not null ? $"{confronto.Nome} · J{j.JogoDaSerie}" : $"R{j.Rodada}",
                    Lado(j.Casa), Lado(j.Visitante), j.Status.ToString(), j.PlacarCasa, j.PlacarVisitante,
                    new TemporadaDoJogo(temporada.Id, temporada.Campeonato.Nome, temporada.Ano), vivo));
            }
        }
        return resultado;
    }

    public sealed record LadoDoJogo(string Texto, bool Definida);
    public sealed record LocalDoJogo(string Nome, string Cidade, string? Estado);
    public sealed record TemporadaDoJogo(Guid Id, string Campeonato, int Ano);
    public sealed record AoVivo(Guid SumulaId, int Periodo, int PlacarCasa, int PlacarVisitante, int SegundosRestantes, bool CronometroAtivo);
    public sealed record CartaoDeJogo(Guid Id, int Numero, string? Data, string? Hora, LocalDoJogo? Local, string Categoria, string Fase, string Etiqueta,
        LadoDoJogo Casa, LadoDoJogo Visitante, string Status, int? PlacarCasa, int? PlacarVisitante, TemporadaDoJogo Temporada, AoVivo? AoVivo);
}
