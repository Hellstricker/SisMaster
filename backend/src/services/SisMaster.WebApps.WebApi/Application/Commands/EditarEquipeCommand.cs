using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EditarEquipeCommand : Command
{
    public Guid EquipeId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cor { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new EditarEquipeCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EditarEquipeCommandValidation : AbstractValidator<EditarEquipeCommand>
{
    public EditarEquipeCommandValidation()
    {
        RuleFor(c => c.EquipeId).NotEmpty().WithMessage("Id da equipe inválido");
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100).WithMessage("Nome deve ter entre 1 e 100 caracteres");
    }
}
