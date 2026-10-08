using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class ExcluirCategoriaCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public Guid CategoriaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ExcluirCategoriaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ExcluirCategoriaCommandValidation : AbstractValidator<ExcluirCategoriaCommand>
{
    public ExcluirCategoriaCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("Id da associação inválido");
        RuleFor(c => c.CategoriaId).NotEmpty().WithMessage("Id da categoria inválido");
    }
}
