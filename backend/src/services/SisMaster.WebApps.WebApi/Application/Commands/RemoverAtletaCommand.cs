using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RemoverAtletaCommand : Command
{
    public Guid EquipeId { get; set; }
    public Guid AtletaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RemoverAtletaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RemoverAtletaCommandValidation : AbstractValidator<RemoverAtletaCommand>
{
    public RemoverAtletaCommandValidation()
    {
        RuleFor(c => c.EquipeId).NotEmpty().WithMessage("Id da equipe inválido");
        RuleFor(c => c.AtletaId).NotEmpty().WithMessage("Id do atleta inválido");
    }
}
