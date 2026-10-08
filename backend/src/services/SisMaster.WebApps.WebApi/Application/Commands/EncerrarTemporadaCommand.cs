using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EncerrarTemporadaCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public Guid TemporadaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new EncerrarTemporadaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EncerrarTemporadaCommandValidation : AbstractValidator<EncerrarTemporadaCommand>
{
    public EncerrarTemporadaCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("Id do campeonato inválido");
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
