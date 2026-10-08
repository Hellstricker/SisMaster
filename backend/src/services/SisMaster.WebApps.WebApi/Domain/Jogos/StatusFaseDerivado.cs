using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>O status da fase não é manual: acompanha os jogos dela.</summary>
public static class StatusFaseDerivado
{
    public static StatusFase De(IReadOnlyCollection<StatusJogo> jogos)
    {
        if (jogos.Count == 0) return StatusFase.Planejada;
        if (jogos.All(s => s is StatusJogo.Encerrado or StatusJogo.WO or StatusJogo.Dispensado)) return StatusFase.Encerrada;
        if (jogos.Any(s => s is StatusJogo.EmAndamento or StatusJogo.Encerrado or StatusJogo.WO)) return StatusFase.EmAndamento;
        return StatusFase.Planejada;
    }
}
