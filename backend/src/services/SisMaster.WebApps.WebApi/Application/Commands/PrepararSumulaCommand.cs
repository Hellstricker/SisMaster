using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Cria a súmula do jogo (estado EmPreparacao), com os dois times ainda sem relação. Exige as duas equipes definidas.</summary>
public class PrepararSumulaCommand : Command
{
    public Guid JogoId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new PrepararSumulaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class PrepararSumulaCommandValidation : AbstractValidator<PrepararSumulaCommand>
{
    public PrepararSumulaCommandValidation()
    {
        RuleFor(c => c.JogoId).NotEmpty().WithMessage("Id do jogo inválido");
    }
}
