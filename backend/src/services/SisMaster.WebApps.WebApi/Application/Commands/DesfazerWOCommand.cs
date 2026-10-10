using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Desfaz um W.O. registrado: o jogo volta a Agendado. O motivo é obrigatório e fica no histórico do jogo.</summary>
public class DesfazerWOCommand : Command
{
    public Guid JogoId { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new DesfazerWOCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class DesfazerWOCommandValidation : AbstractValidator<DesfazerWOCommand>
{
    public DesfazerWOCommandValidation()
    {
        RuleFor(c => c.JogoId).NotEmpty().WithMessage("Id do jogo inválido");
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Informe o motivo para desfazer o W.O.")
            .MaximumLength(300).WithMessage("O motivo deve ter até 300 caracteres");
    }
}
