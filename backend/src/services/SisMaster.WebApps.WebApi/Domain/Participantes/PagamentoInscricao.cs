using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

public enum TipoPagamento
{
    /// <summary>Primeiro pagamento (taxa fixa da temporada), feito ao ter a inscrição aceita.</summary>
    Taxa,

    /// <summary>Pagamento do valor final cobrado após o fim das inscrições (abate o saldo).</summary>
    Saldo
}

public class PagamentoInscricao : Entity
{
    public Guid InscricaoId { get; private set; }
    public TipoPagamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public DateOnly Data { get; private set; }

    protected PagamentoInscricao() { }

    internal PagamentoInscricao(Guid inscricaoId, TipoPagamento tipo, decimal valor, DateOnly data)
    {
        Validacoes.ValidarSeMenorQue(valor, 0.01m, "Valor do pagamento deve ser maior que zero");
        InscricaoId = inscricaoId;
        Tipo = tipo;
        Valor = valor;
        Data = data;
    }
}
