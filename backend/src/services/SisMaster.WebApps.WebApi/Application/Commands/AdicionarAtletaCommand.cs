using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class AdicionarAtletaCommand : Command
{
    public Guid EquipeId { get; set; }

    /// <summary>Pedido de inscrição efetivado que vira atleta da equipe.</summary>
    public Guid InscricaoCategoriaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AdicionarAtletaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AdicionarAtletaCommandValidation : AbstractValidator<AdicionarAtletaCommand>
{
    public AdicionarAtletaCommandValidation()
    {
        RuleFor(c => c.EquipeId).NotEmpty().WithMessage("Id da equipe inválido");
        RuleFor(c => c.InscricaoCategoriaId).NotEmpty().WithMessage("Inscrição é obrigatória");
    }
}
