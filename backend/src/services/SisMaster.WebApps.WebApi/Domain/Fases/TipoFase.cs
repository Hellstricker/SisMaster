namespace SisMaster.WebApps.WebApi.Domain.Fases;

public enum TipoFase
{
    PontosCorridos,
    Grupos,
    MataMata
}

public enum StatusFase
{
    Planejada,
    EmAndamento,
    Encerrada
}

/// <summary>Como as equipes são distribuídas nos grupos a partir da classificação (ou ordem) de entrada.</summary>
public enum DistribuicaoEquipes
{
    /// <summary>1º-A, 2º-B, 3º-B, 4º-A, 5º-A… (ex.: 8 equipes, 2 grupos: A=1,4,5,8 e B=2,3,6,7).</summary>
    Serpentina,

    /// <summary>1º-A, 2º-B, 3º-A, 4º-B… (A=1,3,5,7 e B=2,4,6,8).</summary>
    Alternada,

    /// <summary>A diretoria escolhe a equipe de cada grupo.</summary>
    Manual
}
