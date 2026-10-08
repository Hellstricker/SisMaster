using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarCategoriaCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int IdadeMinima { get; set; }
    public Sexo? Sexo { get; set; }
    public bool AceitaAbaixoIdadeMinima { get; set; }
    public int MinimoPeriodosEmQuadra { get; set; } = 1;
    public int MinimoPeriodosForaQuadra { get; set; } = 1;

    public override bool EhValido()
    {
        ValidationResult = new CriarCategoriaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarCategoriaCommandValidation : AbstractValidator<CriarCategoriaCommand>
{
    public CriarCategoriaCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("Id da associação inválido");
        RuleFor(c => c.Nome).NotEmpty().Length(1, 100).WithMessage("Nome deve ter entre 1 e 100 caracteres");
        RuleFor(c => c.IdadeMinima).InclusiveBetween(0, 120).WithMessage("Idade mínima inválida");
        RuleFor(c => c.MinimoPeriodosEmQuadra).InclusiveBetween(0, 4).WithMessage("Mínimo em quadra deve estar entre 0 e 4");
        RuleFor(c => c.MinimoPeriodosForaQuadra).InclusiveBetween(0, 4).WithMessage("Mínimo fora da quadra deve estar entre 0 e 4");
    }
}
