using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class GerarCobrancasCommand : Command
{
    public Guid TemporadaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new GerarCobrancasCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class GerarCobrancasCommandValidation : AbstractValidator<GerarCobrancasCommand>
{
    public GerarCobrancasCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
