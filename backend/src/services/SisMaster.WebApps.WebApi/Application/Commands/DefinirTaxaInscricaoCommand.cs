using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class DefinirTaxaInscricaoCommand : Command
{
    public Guid TemporadaId { get; set; }

    /// <summary>Nulo = a definir.</summary>
    public decimal? Taxa { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new DefinirTaxaInscricaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class DefinirTaxaInscricaoCommandValidation : AbstractValidator<DefinirTaxaInscricaoCommand>
{
    public DefinirTaxaInscricaoCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.Taxa).GreaterThanOrEqualTo(0).When(c => c.Taxa.HasValue).WithMessage("A taxa não pode ser negativa");
    }
}
