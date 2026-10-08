using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class ConfigurarElegibilidadeCategoriaCommand : Command
{
    public Guid TemporadaId { get; set; }
    public Guid CategoriaId { get; set; }
    public int IdadeMinima { get; set; }

    /// <summary>Nulo = categoria mista.</summary>
    public Sexo? Sexo { get; set; }
    public bool AceitaAbaixoIdadeMinima { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ConfigurarElegibilidadeCategoriaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ConfigurarElegibilidadeCategoriaCommandValidation : AbstractValidator<ConfigurarElegibilidadeCategoriaCommand>
{
    public ConfigurarElegibilidadeCategoriaCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.CategoriaId).NotEmpty().WithMessage("Id da categoria inválido");
        RuleFor(c => c.IdadeMinima).InclusiveBetween(0, 120).WithMessage("Idade mínima inválida");
    }
}
