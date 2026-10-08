using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarTemporadaCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public int Ano { get; set; }
    public DateTime DataInicioInscricoes { get; set; }
    public DateTime DataFimInscricoes { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new CriarTemporadaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarTemporadaCommandValidation : AbstractValidator<CriarTemporadaCommand>
{
    public CriarTemporadaCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("Id do campeonato inválido");
        RuleFor(c => c.Ano).InclusiveBetween(2000, 2100).WithMessage("Ano inválido");
        RuleFor(c => c.DataInicioInscricoes).NotEmpty().WithMessage("Data início das inscrições é obrigatória");
        RuleFor(c => c.DataFimInscricoes)
            .NotEmpty().WithMessage("Data fim das inscrições é obrigatória")
            .GreaterThan(c => c.DataInicioInscricoes).WithMessage("Data fim deve ser posterior à data início");
    }
}
