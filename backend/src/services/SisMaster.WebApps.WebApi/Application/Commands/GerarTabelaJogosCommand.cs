using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Monta a tabela de jogos da temporada inteira a partir das fases (cadastro de fases encerrado). Definitivo.</summary>
public class GerarTabelaJogosCommand : Command
{
    public Guid TemporadaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new GerarTabelaJogosCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class GerarTabelaJogosCommandValidation : AbstractValidator<GerarTabelaJogosCommand>
{
    public GerarTabelaJogosCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
    }
}
