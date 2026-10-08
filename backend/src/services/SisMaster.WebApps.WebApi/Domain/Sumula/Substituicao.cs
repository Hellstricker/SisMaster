using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Domain.Sumula;

/// <summary>
/// Troca de um jogador por outro do mesmo time. É a base do rodízio: a quadra começa com os titulares e só muda por
/// substituição. <see cref="NoInicio"/> = o relógio do período ainda não tinha corrido (ex.: no intervalo), então a
/// troca vale como início do período; fora disso é "no meio" e deixa os dois jogadores com o período parcial.
/// </summary>
public class Substituicao : Entity
{
    public Guid SumulaId { get; private set; }

    /// <summary>Ordem de registro na súmula (1, 2, 3…).</summary>
    public int Ordem { get; private set; }
    /// <summary>Nulo quando o jogador apenas entra para completar uma quadra que começou com menos de 5.</summary>
    public Guid? JogadorSaiId { get; private set; }
    public Guid JogadorEntraId { get; private set; }
    public Periodo Periodo { get; private set; }
    public int TempoJogoSegundos { get; private set; }
    public bool NoInicio { get; private set; }
    public DateTime RegistradoEm { get; private set; }

    protected Substituicao() { }

    internal Substituicao(Guid sumulaId, int ordem, Guid? saiId, Guid entraId, Periodo periodo, int tempoJogoSegundos, bool noInicio)
    {
        Validacoes.ValidarSeMenorQue(tempoJogoSegundos, 0, "Tempo de jogo não pode ser negativo");
        SumulaId = sumulaId;
        Ordem = ordem;
        JogadorSaiId = saiId;
        JogadorEntraId = entraId;
        Periodo = periodo;
        TempoJogoSegundos = tempoJogoSegundos;
        NoInicio = noInicio;
        RegistradoEm = DateTime.UtcNow;
    }
}
