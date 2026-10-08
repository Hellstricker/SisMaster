using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class DefinirValorCategoriaCommand : Command
{
    public Guid TemporadaId { get; set; }
    public Guid CategoriaId { get; set; }
    public decimal? Valor { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new DefinirValorCategoriaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class DefinirValorCategoriaCommandValidation : AbstractValidator<DefinirValorCategoriaCommand>
{
    public DefinirValorCategoriaCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.CategoriaId).NotEmpty().WithMessage("Id da categoria inválido");
        RuleFor(c => c.Valor).GreaterThanOrEqualTo(0).When(c => c.Valor.HasValue).WithMessage("Valor não pode ser negativo");
    }
}
