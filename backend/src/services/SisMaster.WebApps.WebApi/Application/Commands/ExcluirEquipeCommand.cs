using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class ExcluirEquipeCommand : Command
{
    public Guid EquipeId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ExcluirEquipeCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ExcluirEquipeCommandValidation : AbstractValidator<ExcluirEquipeCommand>
{
    public ExcluirEquipeCommandValidation()
    {
        RuleFor(c => c.EquipeId).NotEmpty().WithMessage("Id da equipe inválido");
    }
}
