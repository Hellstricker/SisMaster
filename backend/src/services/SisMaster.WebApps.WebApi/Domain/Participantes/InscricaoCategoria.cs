using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

/// <summary>
/// Pedido de inscrição em uma categoria da temporada. Cada pedido evolui de forma independente:
/// Pendente → AguardandoPagamento (aprovado) → Efetivada (taxa da ficha paga), ou Pendente → Recusada
/// (definitiva; o registro é mantido como histórico e a pessoa pode solicitar uma nova inscrição).
/// O dinheiro é tratado na ficha (<see cref="Inscricao"/>): taxa, cobrança final e saldo.
/// </summary>
public class InscricaoCategoria : Entity
{
    public Guid InscricaoId { get; private set; }
    public Guid TemporadaCategoriaId { get; private set; }
    public StatusInscricaoCategoria Status { get; private set; }

    /// <summary>Preenchida pela diretoria ao aprovar quando idade/sexo fogem do esperado.</summary>
    public string? JustificativaExcecao { get; private set; }

    public DateTime? RecusadaEm { get; private set; }
    public string? MotivoRecusa { get; private set; }

    public Inscricao Inscricao { get; private set; } = null!;
    public TemporadaCategoria TemporadaCategoria { get; private set; } = null!;

    protected InscricaoCategoria() { }

    internal InscricaoCategoria(Guid inscricaoId, Guid temporadaCategoriaId)
    {
        InscricaoId = inscricaoId;
        TemporadaCategoriaId = temporadaCategoriaId;
        Status = StatusInscricaoCategoria.Pendente;
    }

    public bool Ativa => Status != StatusInscricaoCategoria.Recusada;

    /// <param name="foraDoEsperado">Idade ou sexo fora do exigido pela categoria.</param>
    internal void Aprovar(string? justificativaExcecao, bool foraDoEsperado)
    {
        if (Status != StatusInscricaoCategoria.Pendente)
            throw new DomainException("Só é possível aprovar uma inscrição pendente");

        if (foraDoEsperado)
        {
            if (string.IsNullOrWhiteSpace(justificativaExcecao))
                throw new DomainException("Fora do esperado para a categoria: informe uma justificativa para aprovar mesmo assim");
            JustificativaExcecao = justificativaExcecao.Trim();
        }

        Status = StatusInscricaoCategoria.AguardandoPagamento;
    }

    internal void Efetivar()
    {
        if (Status != StatusInscricaoCategoria.AguardandoPagamento)
            throw new DomainException("Esta inscrição não está aguardando pagamento");
        Status = StatusInscricaoCategoria.Efetivada;
    }

    public void Recusar(string motivo)
    {
        if (Status != StatusInscricaoCategoria.Pendente)
            throw new DomainException("Só é possível recusar uma inscrição pendente");
        Validacoes.ValidarSeVazio(motivo, "Informe o motivo da recusa");

        MotivoRecusa = motivo.Trim();
        RecusadaEm = DateTime.UtcNow;
        Status = StatusInscricaoCategoria.Recusada;
    }
}
