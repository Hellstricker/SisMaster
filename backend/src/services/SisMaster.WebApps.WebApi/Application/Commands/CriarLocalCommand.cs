using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarLocalCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string? Estado { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new CriarLocalCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarLocalCommandValidation : AbstractValidator<CriarLocalCommand>
{
    public CriarLocalCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("Id da associação inválido");
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100).WithMessage("Nome deve ter entre 1 e 100 caracteres");
        RuleFor(c => c.Cidade).NotEmpty().MaximumLength(100).WithMessage("Cidade deve ter entre 1 e 100 caracteres");
    }
}
