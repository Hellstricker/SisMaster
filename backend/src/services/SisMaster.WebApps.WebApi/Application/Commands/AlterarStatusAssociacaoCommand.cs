using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class AlterarStatusAssociacaoCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public bool Ativa { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AlterarStatusAssociacaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AlterarStatusAssociacaoCommandValidation : AbstractValidator<AlterarStatusAssociacaoCommand>
{
    public AlterarStatusAssociacaoCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("AssociacaoId é obrigatório");
    }
}
