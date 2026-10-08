namespace SisMaster.WebApps.WebApi.Domain.Jogos;

public enum StatusJogo
{
    Agendado,
    EmAndamento,
    Encerrado,
    WO,

    /// <summary>Jogo "se necessário" de uma série que já foi decidida: não será disputado.</summary>
    Dispensado
}
