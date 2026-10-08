using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class AlterarInscricoesHabilitadasCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public Guid TemporadaId { get; set; }
    public bool Habilitadas { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AlterarInscricoesHabilitadasCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AlterarInscricoesHabilitadasCommandValidation : AbstractValidator<AlterarInscricoesHabilitadasCommand>
{
    public AlterarInscricoesHabilitadasCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("Id do campeonato inválido");
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
