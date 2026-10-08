using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>
/// Agenda vários jogos no mesmo dia, na ordem de <see cref="JogoIds"/>: o primeiro da lista começa em <see cref="HoraInicial"/>
/// e cada seguinte vem <see cref="IntervaloMinutos"/> depois. Tudo ou nada.
/// </summary>
public class AgendarJogosEmLoteCommand : Command
{
    public Guid TemporadaId { get; set; }
    /// <summary>Jogos na ordem em que serão realizados (a ordem define os horários).</summary>
    public List<Guid> JogoIds { get; set; } = [];
    public DateOnly Data { get; set; }
    public TimeOnly HoraInicial { get; set; }
    public int IntervaloMinutos { get; set; }
    public Guid? LocalId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AgendarJogosEmLoteCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AgendarJogosEmLoteCommandValidation : AbstractValidator<AgendarJogosEmLoteCommand>
{
    public AgendarJogosEmLoteCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.JogoIds).NotEmpty().WithMessage("Selecione ao menos um jogo");
        RuleFor(c => c.JogoIds.Count).LessThanOrEqualTo(200).WithMessage("Selecione no máximo 200 jogos por vez");
        RuleFor(c => c.IntervaloMinutos).InclusiveBetween(0, 600).WithMessage("O intervalo deve estar entre 0 e 600 minutos");
    }
}
