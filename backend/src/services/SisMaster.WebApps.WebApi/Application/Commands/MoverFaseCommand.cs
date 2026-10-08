using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class MoverFaseCommand : Command
{
    public Guid FaseId { get; set; }

    /// <summary>-1 sobe uma posição; +1 desce.</summary>
    public int Direcao { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new MoverFaseCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class MoverFaseCommandValidation : AbstractValidator<MoverFaseCommand>
{
    public MoverFaseCommandValidation()
    {
        RuleFor(c => c.FaseId).NotEmpty().WithMessage("Id da fase inválido");
        RuleFor(c => c.Direcao).Must(d => d is -1 or 1).WithMessage("Direção inválida");
    }
}
