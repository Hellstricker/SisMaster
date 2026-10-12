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
        // A bonificação é decimal (ex.: 0,5 ponto por atleta), guardada com 2 casas: mais que isso seria arredondado em silêncio.
        RuleFor(c => c.ValorPorAtleta).Must(v => decimal.Round(v, 2) == v).WithMessage("A bonificação aceita até 2 casas decimais (ex.: 0,5 ou 1,25)");
    }
}
