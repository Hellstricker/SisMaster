using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Classificação da fase (Tela 12), calculada na hora a partir dos jogos encerrados e dos W.O.; nada é gravado.
/// Pontos corridos → uma tabela; Grupos → uma tabela por grupo + ranking dos melhores (N+1)º; Mata-mata → placar das séries.
/// A bonificação por rodízio ainda não existe nas súmulas (Tela 11B): vale 0 até lá.
/// </summary>
public class ClassificacaoFaseQuery
{
    private readonly IFaseRepository _faseRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly BonificacaoDosJogos _bonificacao;

    public ClassificacaoFaseQuery(IFaseRepository faseRepository, IJogoRepository jogoRepository, IEquipeRepository equipeRepository, BonificacaoDosJogos bonificacao)
    {
        _bonificacao = bonificacao;
        _faseRepository = faseRepository;
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<object?> ExecutarAsync(Guid faseId, CancellationToken ct)
    {
        var fase = await _faseRepository.ObterPorIdAsync(faseId, ct);
        if (fase is null) return null;

        var categoria = fase.TemporadaCategoria;
        var temporada = categoria.Temporada;
        var fasesDaCategoria = await _faseRepository.ListarPorTemporadaCategoriaAsync(categoria.Id, ct);
        var confrontosDaCategoria = await _jogoRepository.ListarConfrontosDaCategoriaAsync(categoria.Id, ct);
        var equipes = (await _equipeRepository.ListarPorTemporadaAsync(temporada.Id, ct))
            .Where(e => e.TemporadaCategoriaId == categoria.Id).ToList();
        var texto = new TextoDeReferencia(equipes, fasesDaCategoria, confrontosDaCategoria);
        var tabela = temporada.TabelaJogosGerada ? await _jogoRepository.ObterTabelaDaFaseAsync(fase.Id, ct) : new TabelaGerada();

        object? corpo = null;
        if (temporada.TabelaJogosGerada)
        {
            var bonificacoes = await _bonificacao.CalcularAsync(tabela.Jogos, [fase], ct);
            var sorteios = await _jogoRepository.ListarSorteiosDaFaseAsync(fase.Id, ct);
            corpo = fase.Tipo switch
            {
                TipoFase.MataMata => MataMata(fase, tabela, confrontosDaCategoria, texto),
                TipoFase.Grupos => Grupos(fase, tabela, texto, bonificacoes, sorteios),
                _ => PontosCorridos(fase, tabela, texto, bonificacoes, sorteios)
            };
        }

        return new
        {
            fase.Id,
            fase.Nome,
            fase.Ordem,
            fase.Tipo,
            Status = StatusFaseDerivado.De(tabela.Jogos.Select(j => j.Status).ToList()),
            Categoria = new { categoria.Id, categoria.Nome },
            Temporada = new { temporada.Id, temporada.Ano, TabelaGerada = temporada.TabelaJogosGerada, temporada.ValorBonificacaoPorAtleta },
            fase.NumeroTurnos,
            fase.NumeroGrupos,
            fase.JogosPorConfronto,
            fase.ClassificadosPrimeiros,
            fase.MelhoresExtras,
            TotalJogos = tabela.Jogos.Count,
            JogosEncerrados = tabela.Jogos.Count(Concluido),
            // A bonificação vem do rodízio das súmulas; 0 na temporada = bonificação desligada.
            BonificacaoAtiva = temporada.ValorBonificacaoPorAtleta > 0,
            Classificacao = corpo
        };
    }

    private static bool Concluido(Jogo j) => TabelaDeClassificacao.Concluido(j);

    private static ResultadoJogo? Resultado(Jogo j, IReadOnlyDictionary<Guid, BonificacaoDoJogo>? bonificacoes = null) => TabelaDeClassificacao.Resultado(j, bonificacoes);

    private static bool Definitiva(IEnumerable<Jogo> jogos) => jogos.All(j => Concluido(j) || j.Status == StatusJogo.Dispensado);

    private static Participante ParticipanteDe(FaseEquipe v, TextoDeReferencia texto) =>
        new(v.Id, texto.Equipe(v.EquipeId)?.Nome ?? texto.Descrever(v.Origem));

    private static object LinhaDto(LinhaClassificacao l, FaseEquipe vaga, TextoDeReferencia texto, int posicao, string? classificado)
    {
        var equipe = texto.Equipe(vaga.EquipeId);
        return new
        {
            Posicao = posicao,
            VagaId = vaga.Id,
            Equipe = equipe is null ? null : new { equipe.Id, equipe.Nome, equipe.Cor },
            Texto = l.Participante.Nome,
            l.Jogos,
            l.Vitorias,
            l.Derrotas,
            l.DerrotasWO,
            l.PontosPorVitorias,
            l.PontosPorDerrotas,
            l.PontosPorWO,
            l.Subtotal,
            l.Bonificacao,
            l.Total,
            l.PontosFeitos,
            l.PontosSofridos,
            l.Saldo,
            Sequencia = new string(l.Sequencia.TakeLast(5).ToArray()),
            l.EmpatePorSorteio,
            l.SorteioDefinido,
            Classificado = classificado
        };
    }

    private static object PontosCorridos(Fase fase, TabelaGerada tabela, TextoDeReferencia texto, IReadOnlyDictionary<Guid, BonificacaoDoJogo> bonificacoes, IReadOnlyList<SorteioDeDesempate> sorteios)
    {
        var vagas = tabela.Vagas.Where(v => v.GrupoId is null).ToList();
        var jogos = tabela.Jogos.Where(j => j.GrupoId is null).ToList();
        var linhas = Classificar(vagas, jogos, texto, bonificacoes, sorteios.FirstOrDefault(x => x.GrupoId is null)?.OrdemDasVagas);
        var direto = fase.ClassificadosPrimeiros ?? linhas.Count;
        var definitiva = Definitiva(jogos);

        return new
        {
            Tipo = "Tabela",
            Definitiva = definitiva,
            PodeDefinirSorteio = definitiva && linhas.Any(l => l.EmpatePorSorteio),
            SorteioDefinido = linhas.Any(l => l.SorteioDefinido),
            Linhas = linhas.Select((l, i) => LinhaDto(l, vagas.First(v => v.Id == l.Participante.Id), texto, i + 1, i < direto ? "Direto" : null)).ToList()
        };
    }

    private static object Grupos(Fase fase, TabelaGerada tabela, TextoDeReferencia texto, IReadOnlyDictionary<Guid, BonificacaoDoJogo> bonificacoes, IReadOnlyList<SorteioDeDesempate> sorteios)
    {
        var tabelas = new List<(Grupo Grupo, List<FaseEquipe> Vagas, List<LinhaClassificacao> Linhas, bool Definitiva)>();
        foreach (var grupo in tabela.Grupos.OrderBy(g => g.Ordem))
        {
            var vagas = tabela.Vagas.Where(v => v.GrupoId == grupo.Id).ToList();
            var jogos = tabela.Jogos.Where(j => j.GrupoId == grupo.Id).ToList();
            tabelas.Add((grupo, vagas, Classificar(vagas, jogos, texto, bonificacoes, sorteios.FirstOrDefault(x => x.GrupoId == grupo.Id)?.OrdemDasVagas), Definitiva(jogos)));
        }

        // Melhores (N+1)º: o (N+1)º de cada grupo disputa as vagas extras.
        var extras = new HashSet<Guid>();
        List<LinhaClassificacao> ranking = [];
        if (fase.ClassificadosPrimeiros is { } n && fase.MelhoresExtras > 0)
        {
            var candidatas = tabelas.Where(t => t.Linhas.Count > n).Select(t => t.Linhas[n]).ToList();
            ranking = ServicoClassificacao.OrdenarMelhores(candidatas).ToList();
            foreach (var l in ranking.Take(fase.MelhoresExtras)) extras.Add(l.Participante.Id);
        }

        var grupos = tabelas.Select(t => new
        {
            t.Grupo.Ordem,
            t.Grupo.Nome,
            t.Definitiva,
            PodeDefinirSorteio = t.Definitiva && t.Linhas.Any(l => l.EmpatePorSorteio),
            SorteioDefinido = t.Linhas.Any(l => l.SorteioDefinido),
            Linhas = t.Linhas.Select((l, i) => LinhaDto(l, t.Vagas.First(v => v.Id == l.Participante.Id), texto, i + 1,
                i < (fase.ClassificadosPrimeiros ?? t.Linhas.Count) ? "Direto" : extras.Contains(l.Participante.Id) ? "Melhor" : null)).ToList()
        }).ToList();

        var melhores = ranking.Select((l, i) =>
        {
            var t = tabelas.First(x => x.Vagas.Any(v => v.Id == l.Participante.Id));
            return new
            {
                Posicao = i + 1,
                Grupo = t.Grupo.Nome,
                Linha = LinhaDto(l, t.Vagas.First(v => v.Id == l.Participante.Id), texto, fase.ClassificadosPrimeiros!.Value + 1, extras.Contains(l.Participante.Id) ? "Melhor" : null)
            };
        }).ToList();

        return new { Tipo = "Grupos", Definitiva = tabelas.All(t => t.Definitiva), Grupos = grupos, Melhores = melhores };
    }

    private static List<LinhaClassificacao> Classificar(List<FaseEquipe> vagas, List<Jogo> jogos, TextoDeReferencia texto, IReadOnlyDictionary<Guid, BonificacaoDoJogo> bonificacoes, IReadOnlyList<Guid>? ordemDoSorteio) =>
        TabelaDeClassificacao.Calcular(vagas, jogos, v => ParticipanteDe(v, texto).Nome, bonificacoes, ordemDoSorteio);

    private static object MataMata(Fase fase, TabelaGerada tabela, IReadOnlyList<Confronto> confrontosDaCategoria, TextoDeReferencia texto)
    {
        var jogosPorConfronto = fase.JogosPorConfronto ?? 1;
        var confrontos = confrontosDaCategoria.Where(c => c.FaseId == fase.Id).OrderBy(c => c.Numero).Select(c =>
        {
            var jogos = tabela.Jogos.Where(j => j.ConfrontoId == c.Id).OrderBy(j => j.JogoDaSerie).ThenBy(j => j.Numero).ToList();
            var primeiro = jogos.FirstOrDefault();
            var ladoA = primeiro?.Casa;
            var ladoB = primeiro?.Visitante;

            var resultados = jogos.Select(j => Resultado(j)).Where(r => r is not null).Select(r => r!).ToList();
            var (sa, sb) = ladoA is null || ladoB is null ? (0, 0) : ServicoSerie.Placar(ladoA.Id, ladoB.Id, resultados);
            var vencedor = ladoA is null || ladoB is null ? null : ServicoSerie.Vencedor(ladoA.Id, ladoB.Id, resultados, jogosPorConfronto);

            object Lado(FaseEquipe? v, ReferenciaEquipe origem, int vitorias)
            {
                var equipe = texto.Equipe(v?.EquipeId);
                return new
                {
                    Equipe = equipe is null ? null : new { equipe.Id, equipe.Nome, equipe.Cor },
                    Texto = equipe?.Nome ?? texto.Descrever(v?.Origem ?? origem),
                    Vitorias = vitorias,
                    Venceu = v is not null && vencedor == v.Id
                };
            }

            return new
            {
                c.Id,
                c.Numero,
                c.Nome,
                Decidido = vencedor is not null,
                Iniciado = resultados.Count > 0,
                A = Lado(ladoA, c.OrigemA, sa),
                B = Lado(ladoB, c.OrigemB, sb),
                Jogos = jogos.Select(j => new
                {
                    j.Id, j.Numero, j.JogoDaSerie, j.Opcional, j.Status, j.PlacarCasa, j.PlacarVisitante
                }).ToList()
            };
        }).ToList();

        return new { Tipo = "MataMata", VitoriasNecessarias = ServicoSerie.VitoriasNecessarias(jogosPorConfronto), Confrontos = confrontos };
    }
}
