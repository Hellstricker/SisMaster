using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class ExcluirFaseCommand : Command
{
    public Guid FaseId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ExcluirFaseCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ExcluirFaseCommandValidation : AbstractValidator<ExcluirFaseCommand>
{
    public ExcluirFaseCommandValidation()
    {
        RuleFor(c => c.FaseId).NotEmpty().WithMessage("Id da fase inválido");
    }
}
