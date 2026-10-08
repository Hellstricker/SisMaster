using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EncerrarCadastroFasesCommand : Command
{
    public Guid TemporadaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new EncerrarCadastroFasesCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EncerrarCadastroFasesCommandValidation : AbstractValidator<EncerrarCadastroFasesCommand>
{
    public EncerrarCadastroFasesCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
