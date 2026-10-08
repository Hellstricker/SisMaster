using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Encerra a súmula; o jogo recebe o placar final e passa a Encerrado.</summary>
public class EncerrarSumulaCommand : Command
{
    public Guid SumulaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new EncerrarSumulaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EncerrarSumulaCommandValidation : AbstractValidator<EncerrarSumulaCommand>
{
    public EncerrarSumulaCommandValidation()
    {
        RuleFor(c => c.SumulaId).NotEmpty().WithMessage("Id da súmula inválido");
    }
}
