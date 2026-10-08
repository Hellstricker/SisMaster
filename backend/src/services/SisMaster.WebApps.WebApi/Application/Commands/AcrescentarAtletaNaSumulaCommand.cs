using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Acrescenta à súmula um atleta do elenco depois do início do jogo (chegou atrasado). Entra no banco, com a camisa informada.</summary>
public class AcrescentarAtletaNaSumulaCommand : Command
{
    public Guid SumulaId { get; set; }
    public LadoTime Lado { get; set; }
    public Guid AtletaId { get; set; }
    public string Numero { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new AcrescentarAtletaNaSumulaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AcrescentarAtletaNaSumulaCommandValidation : AbstractValidator<AcrescentarAtletaNaSumulaCommand>
{
    public AcrescentarAtletaNaSumulaCommandValidation()
    {
        RuleFor(c => c.SumulaId).NotEmpty().WithMessage("Súmula inválida");
        RuleFor(c => c.Lado).IsInEnum().WithMessage("Lado inválido");
        RuleFor(c => c.AtletaId).NotEmpty().WithMessage("Informe o atleta");
        RuleFor(c => c.Numero).NotEmpty().WithMessage("Informe a camisa do atleta");
    }
}
