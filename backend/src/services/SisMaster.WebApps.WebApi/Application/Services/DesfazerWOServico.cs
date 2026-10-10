using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// Quando um W.O. pode ser desfeito: só enquanto nada foi calculado a partir do resultado. Se a fase já definiu vagas da
/// fase seguinte (ou o confronto já definiu o vencedor de outra vaga), ou há sorteio de desempate registrado na fase,
/// desfazer deixaria a temporada inconsistente. Usado pelo comando (que recusa) e pela tela (que mostra o motivo).
/// </summary>
public class DesfazerWOServico
{
    private readonly IJogoRepository _jogoRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly ITemporadaRepository _temporadaRepository;

    public DesfazerWOServico(IJogoRepository jogoRepository, IFaseRepository faseRepository, ITemporadaRepository temporadaRepository)
    {
        _jogoRepository = jogoRepository;
        _faseRepository = faseRepository;
        _temporadaRepository = temporadaRepository;
    }

    /// <summary>O motivo que impede desfazer o W.O. do jogo, ou nulo se pode.</summary>
    public async Task<string?> MotivoQueImpedeAsync(Jogo jogo, CancellationToken ct = default)
    {
        if (jogo.Status != StatusJogo.WO) return "O jogo não está em W.O.";

        var temporada = await _temporadaRepository.ObterPorIdAsync(jogo.TemporadaId, ct);
        if (temporada is null || temporada.Status == StatusTemporada.Encerrada)
            return "Temporada encerrada não aceita alteração de jogos";

        if ((await _jogoRepository.ListarSorteiosDaFaseAsync(jogo.FaseId, ct)).Count > 0)
            return "Não é possível desfazer: há um sorteio de desempate registrado nesta fase e ele depende da classificação com este W.O.";

        var fase = await _faseRepository.ObterPorIdAsync(jogo.FaseId, ct);
        if (fase is not null && fase.Tipo != TipoFase.MataMata && await _jogoRepository.ExisteVagaResolvidaPelaColocacaoAsync(fase.Id, ct))
            return "Não é possível desfazer: a fase já terminou e definiu as equipes da fase seguinte a partir deste resultado. Corrija a fase seguinte antes, ou fale com a diretoria.";

        if (jogo.ConfrontoId is { } confronto && await _jogoRepository.ExisteVagaResolvidaPeloConfrontoAsync(confronto, ct))
            return "Não é possível desfazer: a série já definiu uma equipe da fase seguinte a partir deste resultado. Corrija a fase seguinte antes, ou fale com a diretoria.";

        return null;
    }
}
