using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class ReabrirCadastroFasesCommand : Command
{
    public Guid TemporadaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ReabrirCadastroFasesCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ReabrirCadastroFasesCommandValidation : AbstractValidator<ReabrirCadastroFasesCommand>
{
    public ReabrirCadastroFasesCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
