using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RegistrarPagamentoTaxaCommand : Command
{
    public Guid InscricaoId { get; set; }
    public DateOnly DataPagamento { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RegistrarPagamentoTaxaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RegistrarPagamentoTaxaCommandValidation : AbstractValidator<RegistrarPagamentoTaxaCommand>
{
    public RegistrarPagamentoTaxaCommandValidation()
    {
        RuleFor(c => c.InscricaoId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.DataPagamento).NotEqual(default(DateOnly)).WithMessage("Data do pagamento inválida");
    }
}
