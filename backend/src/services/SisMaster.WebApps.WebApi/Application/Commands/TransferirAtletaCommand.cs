using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Passa um atleta para outra equipe da mesma categoria; com <see cref="AtletaTrocaId"/>, troca por um atleta da outra equipe.</summary>
public class TransferirAtletaCommand : Command
{
    public Guid EquipeId { get; set; }
    public Guid AtletaId { get; set; }
    public Guid EquipeDestinoId { get; set; }

    /// <summary>Atleta da equipe de destino que vem para a equipe de origem (opcional).</summary>
    public Guid? AtletaTrocaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new TransferirAtletaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class TransferirAtletaCommandValidation : AbstractValidator<TransferirAtletaCommand>
{
    public TransferirAtletaCommandValidation()
    {
        RuleFor(c => c.EquipeId).NotEmpty().WithMessage("Id da equipe inválido");
        RuleFor(c => c.AtletaId).NotEmpty().WithMessage("Id do atleta inválido");
        RuleFor(c => c.EquipeDestinoId).NotEmpty().WithMessage("Escolha a equipe de destino");
        RuleFor(c => c.AtletaTrocaId).NotEqual(Guid.Empty).When(c => c.AtletaTrocaId.HasValue).WithMessage("Atleta da troca inválido");
    }
}
