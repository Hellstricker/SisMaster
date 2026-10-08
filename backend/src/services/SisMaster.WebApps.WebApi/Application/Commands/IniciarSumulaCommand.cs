using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Inicia a súmula do jogo: exige as duas relações completas, trava a relação e põe o jogo em andamento.</summary>
public class IniciarSumulaCommand : Command
{
    public Guid JogoId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new IniciarSumulaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class IniciarSumulaCommandValidation : AbstractValidator<IniciarSumulaCommand>
{
    public IniciarSumulaCommandValidation()
    {
        RuleFor(c => c.JogoId).NotEmpty().WithMessage("Id do jogo inválido");
    }
}
