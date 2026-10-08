using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Tudo que a Tela 8 (detalhe da fase) precisa. Com a tabela de jogos gerada, mostra o que está gravado;
/// antes disso, roda a geração em memória (sem gravar) para mostrar uma prévia de grupos, vagas e jogos.
/// </summary>
public class DetalheFaseQuery
{
    private readonly IFaseRepository _faseRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ITemporadaRepository _temporadaRepository;

    public DetalheFaseQuery(IFaseRepository faseRepository, IJogoRepository jogoRepository, IEquipeRepository equipeRepository, ITemporadaRepository temporadaRepository)
    {
        _faseRepository = faseRepository;
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
        _temporadaRepository = temporadaRepository;
    }

    public async Task<object?> ExecutarAsync(Guid faseId, CancellationToken ct)
    {
        var fase = await _faseRepository.ObterPorIdAsync(faseId, ct);
        if (fase is null) return null;

        var categoria = fase.TemporadaCategoria;
        var temporada = categoria.Temporada;
        var associacaoId = (await _temporadaRepository.ObterPorIdAsync(temporada.Id, ct))!.Campeonato.AssociacaoId;

        var fasesDaCategoria = await _faseRepository.ListarPorTemporadaCategoriaAsync(categoria.Id, ct);
        var confrontosDaCategoria = await _jogoRepository.ListarConfrontosDaCategoriaAsync(categoria.Id, ct);
        var equipes = (await _equipeRepository.ListarPorTemporadaAsync(temporada.Id, ct))
            .Where(e => e.TemporadaCategoriaId == categoria.Id).OrderBy(e => e.Nome).ToList();

        TabelaGerada tabela;
        string? previaErro = null;
        var gerada = temporada.TabelaJogosGerada;
        if (gerada)
        {
            tabela = await _jogoRepository.ObterTabelaDaFaseAsync(fase.Id, ct);
        }
        else
        {
            tabela = new TabelaGerada();
            try
            {
                // Fases seguintes não influenciam esta (as origens só vão para a frente): ficam de fora da prévia,
                // assim uma fase posterior incompleta não esconde a prévia desta.
                var ateEstaFase = fasesDaCategoria.Where(f => f.Ordem <= fase.Ordem).ToList();
                var completa = ServicoGeracaoJogos.Gerar(temporada.Id,
                    [new CategoriaParaGerar(categoria.Id, categoria.Nome, equipes, ateEstaFase, confrontosDaCategoria)]);
                tabela.Grupos.AddRange(completa.Grupos.Where(g => g.FaseId == fase.Id));
                tabela.Vagas.AddRange(completa.Vagas.Where(v => v.FaseId == fase.Id).OrderBy(v => v.Posicao));
                tabela.Jogos.AddRange(completa.Jogos.Where(j => j.FaseId == fase.Id));
            }
            catch (DomainException ex)
            {
                previaErro = ex.Message;
            }
        }

        var texto = new TextoDeReferencia(equipes, fasesDaCategoria, confrontosDaCategoria);
        var gruposPorId = tabela.Grupos.ToDictionary(g => g.Id);
        var vagas = tabela.Vagas.Select(v => VagaDto(v, texto, gruposPorId)).ToList();
        var jogos = tabela.Jogos
            .OrderBy(j => gerada ? j.Numero : 0).ThenBy(j => j.Rodada)
            .Select(j => JogoDto(j, gerada, texto, gruposPorId)).ToList();

        var confrontos = confrontosDaCategoria.Where(c => c.FaseId == fase.Id).OrderBy(c => c.Numero).Select(c => new
        {
            c.Id,
            c.Numero,
            c.Nome,
            OrigemA = OrigemDto(c.OrigemA, texto),
            OrigemB = OrigemDto(c.OrigemB, texto),
            Jogos = jogos.Where(j => j.ConfrontoId == c.Id).ToList()
        }).ToList();

        var definidos = confrontos.Select(c => c.Numero).ToHashSet();
        var confrontosPendentes = fase.Tipo == TipoFase.MataMata
            ? Enumerable.Range(1, fase.NumeroConfrontos ?? 0).Where(n => !definidos.Contains(n)).ToList()
            : [];

        var status = StatusFaseDerivado.De(tabela.Jogos.Select(j => j.Status).ToList());
        var fasesAnteriores = fasesDaCategoria.Where(f => f.Ordem < fase.Ordem).OrderBy(f => f.Ordem).Select(f => new
        {
            f.Id,
            f.Nome,
            f.Ordem,
            f.Tipo,
            f.NumeroGrupos,
            Confrontos = confrontosDaCategoria.Where(c => c.FaseId == f.Id).OrderBy(c => c.Numero).Select(c => new { c.Id, c.Numero, c.Nome })
        });

        return new
        {
            fase.Id,
            fase.Nome,
            fase.Ordem,
            fase.Tipo,
            Status = status,
            AssociacaoId = associacaoId,
            Categoria = new { categoria.Id, categoria.Nome },
            Temporada = new { temporada.Id, temporada.Ano, temporada.Status, temporada.CadastroFasesEncerrado, TabelaGerada = gerada },
            fase.NumeroTurnos,
            fase.NumeroGrupos,
            fase.Distribuicao,
            DistribuicaoManualDefinida = fase.DistribuicaoManual is not null,
            fase.JogosPorConfronto,
            fase.NumeroConfrontos,
            fase.ClassificadosPrimeiros,
            fase.MelhoresExtras,
            fase.FaseAnteriorId,
            FaseAnteriorNome = fasesDaCategoria.FirstOrDefault(f => f.Id == fase.FaseAnteriorId)?.Nome,
            EquipesDaCategoria = equipes.Select(e => new { e.Id, e.Nome, e.Cor }),
            // Os cruzamentos só podem mudar até a tabela de jogos ser gerada.
            PermiteEditarCruzamentos = !gerada && temporada.Status is StatusTemporada.InscricoesEncerradas or StatusTemporada.EmAndamento,
            Previa = !gerada,
            PreviaErro = previaErro,
            Grupos = tabela.Grupos.OrderBy(g => g.Ordem).Select(g => new
            {
                g.Ordem,
                g.Nome,
                Equipes = vagas.Where(v => v.GrupoOrdem == g.Ordem).OrderBy(v => v.Posicao).ToList()
            }),
            Vagas = vagas,
            Confrontos = confrontos,
            ConfrontosPendentes = confrontosPendentes,
            FasesAnteriores = fasesAnteriores,
            Jogos = jogos,
            TotalJogos = jogos.Count
        };
    }

    private static object? EquipeDto(Equipe? e) => e is null ? null : new { e.Id, e.Nome, e.Cor };

    private static object OrigemDto(ReferenciaEquipe r, TextoDeReferencia texto) => new
    {
        r.Tipo, r.EquipeId, r.FaseId, r.GrupoOrdem, r.Posicao, ConfrontoId = r.ConfrontoOrigemId,
        Texto = texto.Descrever(r)
    };

    private static VagaView VagaDto(FaseEquipe v, TextoDeReferencia texto, IReadOnlyDictionary<Guid, Grupo> grupos) => new()
    {
        Id = v.Id,
        Posicao = v.Posicao,
        GrupoOrdem = v.GrupoId is null ? null : grupos[v.GrupoId.Value].Ordem,
        Origem = OrigemDto(v.Origem, texto),
        Equipe = EquipeDto(texto.Equipe(v.EquipeId)),
        Texto = v.EquipeId is not null ? texto.Equipe(v.EquipeId)?.Nome ?? "—" : texto.Descrever(v.Origem)
    };

    private static JogoView JogoDto(Jogo j, bool gerada, TextoDeReferencia texto, IReadOnlyDictionary<Guid, Grupo> grupos) => new()
    {
        Id = gerada ? j.Id : null,
        Numero = gerada ? j.Numero : null,
        Rodada = j.Rodada,
        JogoDaSerie = j.JogoDaSerie,
        Opcional = j.Opcional,
        Grupo = j.GrupoId is null ? null : grupos[j.GrupoId.Value].Nome,
        ConfrontoId = j.ConfrontoId,
        Casa = LadoDto(j.Casa, texto),
        Visitante = LadoDto(j.Visitante, texto),
        Data = j.Data,
        Hora = j.Hora,
        Local = j.Local is null ? null : new { j.Local.Id, j.Local.Nome, j.Local.Cidade, j.Local.Estado },
        Status = j.Status,
        PlacarCasa = j.PlacarCasa,
        PlacarVisitante = j.PlacarVisitante
    };

    private static object LadoDto(FaseEquipe v, TextoDeReferencia texto)
    {
        var equipe = texto.Equipe(v.EquipeId);
        return new { Texto = equipe?.Nome ?? texto.Descrever(v.Origem), Equipe = EquipeDto(equipe), v.Posicao };
    }

    public sealed class VagaView
    {
        public Guid Id { get; init; }
        public int Posicao { get; init; }
        public int? GrupoOrdem { get; init; }
        public object? Origem { get; init; }
        public object? Equipe { get; init; }
        public string Texto { get; init; } = string.Empty;
    }

    public sealed class JogoView
    {
        public Guid? Id { get; init; }
        public int? Numero { get; init; }
        public int Rodada { get; init; }
        public int? JogoDaSerie { get; init; }
        public bool Opcional { get; init; }
        public string? Grupo { get; init; }
        public Guid? ConfrontoId { get; init; }
        public object? Casa { get; init; }
        public object? Visitante { get; init; }
        public DateOnly? Data { get; init; }
        public TimeOnly? Hora { get; init; }
        public object? Local { get; init; }
        public StatusJogo Status { get; init; }
        public int? PlacarCasa { get; init; }
        public int? PlacarVisitante { get; init; }
    }
}
