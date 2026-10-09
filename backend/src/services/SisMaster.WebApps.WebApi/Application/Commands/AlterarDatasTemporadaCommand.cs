using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Muda a janela de inscrições e as datas (opcionais) de realização do campeonato.</summary>
public class AlterarDatasTemporadaCommand : Command
{
    public Guid TemporadaId { get; set; }
    public DateTime DataInicioInscricoes { get; set; }
    public DateTime DataFimInscricoes { get; set; }
    public DateTime? DataInicioCompeticao { get; set; }
    public DateTime? DataFimCompeticao { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AlterarDatasTemporadaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AlterarDatasTemporadaCommandValidation : AbstractValidator<AlterarDatasTemporadaCommand>
{
    public AlterarDatasTemporadaCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.DataInicioInscricoes).NotEmpty().WithMessage("Data início das inscrições é obrigatória");
        RuleFor(c => c.DataFimInscricoes)
            .NotEmpty().WithMessage("Data fim das inscrições é obrigatória")
            .GreaterThan(c => c.DataInicioInscricoes).WithMessage("Data fim deve ser posterior à data início");
        RuleFor(c => c.DataFimCompeticao)
            .GreaterThanOrEqualTo(c => c.DataInicioCompeticao!.Value)
            .When(c => c.DataInicioCompeticao.HasValue && c.DataFimCompeticao.HasValue)
            .WithMessage("A data de fim da competição não pode ser anterior à de início");
    }
}
