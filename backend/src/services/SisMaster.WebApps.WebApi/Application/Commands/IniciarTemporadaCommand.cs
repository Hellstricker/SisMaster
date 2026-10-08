using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class IniciarTemporadaCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public Guid TemporadaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new IniciarTemporadaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class IniciarTemporadaCommandValidation : AbstractValidator<IniciarTemporadaCommand>
{
    public IniciarTemporadaCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("Id do campeonato inválido");
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
