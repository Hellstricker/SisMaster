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

    /// <summary>Pagamento estornado: continua no histórico, mas deixa de contar no total pago.</summary>
    public DateTime? EstornadoEm { get; private set; }
    public string? MotivoEstorno { get; private set; }
    public string? EstornadoPor { get; private set; }

    public bool Ativo => EstornadoEm is null;

    protected PagamentoInscricao() { }

    internal PagamentoInscricao(Guid inscricaoId, TipoPagamento tipo, decimal valor, DateOnly data)
    {
        Validacoes.ValidarSeMenorQue(valor, 0.01m, "Valor do pagamento deve ser maior que zero");
        InscricaoId = inscricaoId;
        Tipo = tipo;
        Valor = valor;
        Data = data;
    }

    internal void Estornar(string motivo, string? usuario)
    {
        if (!Ativo)
            throw new DomainException("Este pagamento já foi estornado");
        Validacoes.ValidarSeVazio(motivo, "Informe o motivo do estorno");
        EstornadoEm = DateTime.UtcNow;
        MotivoEstorno = motivo.Trim();
        EstornadoPor = usuario;
    }
}
