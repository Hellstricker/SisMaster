using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class ConfigurarBonificacaoCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public Guid TemporadaId { get; set; }
    public decimal ValorPorAtleta { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ConfigurarBonificacaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ConfigurarBonificacaoCommandValidation : AbstractValidator<ConfigurarBonificacaoCommand>
{
    public ConfigurarBonificacaoCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("Id do campeonato inválido");
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.ValorPorAtleta).InclusiveBetween(0, 9999).WithMessage("Valor da bonificação inválido");
    }
}
