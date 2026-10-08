using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EditarLocalCommand : Command
{
    public Guid LocalId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string? Estado { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new EditarLocalCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EditarLocalCommandValidation : AbstractValidator<EditarLocalCommand>
{
    public EditarLocalCommandValidation()
    {
        RuleFor(c => c.LocalId).NotEmpty().WithMessage("Id do local inválido");
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100).WithMessage("Nome deve ter entre 1 e 100 caracteres");
        RuleFor(c => c.Cidade).NotEmpty().MaximumLength(100).WithMessage("Cidade deve ter entre 1 e 100 caracteres");
    }
}

public class ExcluirLocalCommand : Command
{
    public Guid LocalId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ValidationResult();
        if (LocalId == Guid.Empty) ValidationResult.Errors.Add(new ValidationFailure(nameof(LocalId), "Id do local inválido"));
        return ValidationResult.IsValid;
    }
}
