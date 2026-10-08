using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Define data, hora e local de um jogo (qualquer um pode ficar vazio, para limpar).</summary>
public class AgendarJogoCommand : Command
{
    public Guid JogoId { get; set; }
    public DateOnly? Data { get; set; }
    public TimeOnly? Hora { get; set; }
    public Guid? LocalId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AgendarJogoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AgendarJogoCommandValidation : AbstractValidator<AgendarJogoCommand>
{
    public AgendarJogoCommandValidation()
    {
        RuleFor(c => c.JogoId).NotEmpty().WithMessage("Id do jogo inválido");
    }
}
