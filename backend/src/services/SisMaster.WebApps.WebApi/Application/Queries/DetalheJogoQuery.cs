using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>Tela 10: o jogo no campeonato (equipes ou origens, agenda, resultado) com o resumo da súmula.</summary>
public class DetalheJogoQuery
{
    private readonly IJogoRepository _jogoRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly ILocalRepository _localRepository;

    public DetalheJogoQuery(IJogoRepository jogoRepository, IFaseRepository faseRepository, IEquipeRepository equipeRepository,
        ISumulaRepository sumulaRepository, ITemporadaRepository temporadaRepository, ILocalRepository localRepository)
    {
        _jogoRepository = jogoRepository;
        _faseRepository = faseRepository;
        _equipeRepository = equipeRepository;
        _sumulaRepository = sumulaRepository;
        _temporadaRepository = temporadaRepository;
        _localRepository = localRepository;
    }

    public async Task<object?> ExecutarAsync(Guid jogoId, CancellationToken ct)
    {
        var jogo = await _jogoRepository.ObterPorIdAsync(jogoId, ct);
        if (jogo is null) return null;

        var fase = (await _faseRepository.ObterPorIdAsync(jogo.FaseId, ct))!;
        var categoria = fase.TemporadaCategoria;
        var temporada = (await _temporadaRepository.ObterPorIdAsync(jogo.TemporadaId, ct))!;

        var fases = await _faseRepository.ListarPorTemporadaCategoriaAsync(categoria.Id, ct);
        var confrontos = await _jogoRepository.ListarConfrontosDaCategoriaAsync(categoria.Id, ct);
        var equipes = (await _equipeRepository.ListarPorTemporadaAsync(temporada.Id, ct)).Where(e => e.TemporadaCategoriaId == categoria.Id).ToList();
        var texto = new TextoDeReferencia(equipes, fases, confrontos);
        var local = jogo.LocalId is null ? null : await _localRepository.ObterPorIdAsync(jogo.LocalId.Value, ct);
        var sumula = jogo.SumulaId is null ? null : await _sumulaRepository.ObterPorJogoAsync(jogo.Id, ct);

        object Lado(FaseEquipe v)
        {
            var equipe = texto.Equipe(v.EquipeId);
            return new { Texto = equipe?.Nome ?? texto.Descrever(v.Origem), Definida = equipe is not null, EquipeId = equipe?.Id, Cor = equipe?.Cor };
        }

        var definidas = jogo.Casa.EquipeId is not null && jogo.Visitante.EquipeId is not null;
        var temporadaAberta = temporada.Status != StatusTemporada.Encerrada;
        var confronto = jogo.ConfrontoId is null ? null : confrontos.FirstOrDefault(c => c.Id == jogo.ConfrontoId);
        var sumulaIniciada = sumula is not null && sumula.Status != Domain.Sumula.Enums.StatusSumula.EmPreparacao;

        string? motivoSemSumula = null;
        if (sumula is null)
        {
            if (!definidas) motivoSemSumula = "As equipes ainda não foram definidas";
            else if (jogo.Status != StatusJogo.Agendado) motivoSemSumula = "O jogo não está mais agendado";
            else if (!temporadaAberta) motivoSemSumula = "A temporada está encerrada";
        }

        return new
        {
            jogo.Id,
            jogo.Numero,
            jogo.Rodada,
            jogo.JogoDaSerie,
            jogo.Opcional,
            Categoria = new { categoria.Id, categoria.Nome },
            Fase = new { fase.Id, fase.Nome, fase.Tipo },
            Etiqueta = confronto is not null ? $"{confronto.Nome} · J{jogo.JogoDaSerie}" : $"R{jogo.Rodada}",
            Temporada = new { temporada.Id, temporada.Ano, temporada.Status },
            AssociacaoId = temporada.Campeonato.AssociacaoId,
            Casa = Lado(jogo.Casa),
            Visitante = Lado(jogo.Visitante),
            jogo.Data,
            jogo.Hora,
            Local = local is null ? null : new { local.Id, local.Nome, local.Cidade, local.Estado },
            jogo.Status,
            jogo.PlacarCasa,
            jogo.PlacarVisitante,
            // W.O.: a equipe ausente é a que ficou com 0.
            WoCasaAusente = jogo.Status == StatusJogo.WO ? jogo.PlacarCasa == 0 : (bool?)null,
            PodeAgendar = jogo.Status == StatusJogo.Agendado && !sumulaIniciada && temporadaAberta,
            PodeRegistrarWO = definidas && jogo.Status == StatusJogo.Agendado && !sumulaIniciada && temporadaAberta,
            PodePrepararSumula = sumula is null && motivoSemSumula is null,
            MotivoSemSumula = motivoSemSumula,
            Sumula = sumula is null ? null : new
            {
                sumula.Id,
                sumula.Status,
                sumula.CodigoExterno,
                sumula.ImportadaEm,
                Times = sumula.Times.OrderBy(t => t.Lado).Select(t => new
                {
                    t.Lado,
                    t.Nome,
                    Relacionados = t.Jogadores.Count,
                    t.Tecnico,
                    Capitao = t.Jogadores.FirstOrDefault(j => j.Id == t.CapitaoId)?.Nome
                })
            }
        };
    }
}
