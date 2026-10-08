using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RegistrarPagamentoSaldoCommand : Command
{
    public Guid InscricaoId { get; set; }
    public decimal Valor { get; set; }
    public DateOnly DataPagamento { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RegistrarPagamentoSaldoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RegistrarPagamentoSaldoCommandValidation : AbstractValidator<RegistrarPagamentoSaldoCommand>
{
    public RegistrarPagamentoSaldoCommandValidation()
    {
        RuleFor(c => c.InscricaoId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.Valor).GreaterThan(0).WithMessage("Valor do pagamento deve ser maior que zero");
        RuleFor(c => c.DataPagamento).NotEqual(default(DateOnly)).WithMessage("Data do pagamento inválida");
    }
}
