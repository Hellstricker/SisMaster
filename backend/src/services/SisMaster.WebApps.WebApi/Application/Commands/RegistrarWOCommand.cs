using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Registra o W.O. de um jogo: a equipe ausente perde por 20 × 0, sem bonificação. Descarta a súmula se ainda só estiver preparada.</summary>
public class RegistrarWOCommand : Command
{
    public Guid JogoId { get; set; }
    public bool CasaAusente { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RegistrarWOCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RegistrarWOCommandValidation : AbstractValidator<RegistrarWOCommand>
{
    public RegistrarWOCommandValidation()
    {
        RuleFor(c => c.JogoId).NotEmpty().WithMessage("Id do jogo inválido");
    }
}
