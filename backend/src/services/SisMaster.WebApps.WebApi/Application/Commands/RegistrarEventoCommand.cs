using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RegistrarEventoCommand : Command
{
    public Guid PartidaId { get; set; }
    public Guid JogadorId { get; set; }
    public TipoEvento Tipo { get; set; }
    public int TempoJogoSegundos { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RegistrarEventoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RegistrarEventoCommandValidation : AbstractValidator<RegistrarEventoCommand>
{
    public RegistrarEventoCommandValidation()
    {
        RuleFor(c => c.PartidaId).NotEmpty().WithMessage("PartidaId é obrigatório");
        RuleFor(c => c.JogadorId).NotEmpty().WithMessage("JogadorId é obrigatório");
        RuleFor(c => c.Tipo).IsInEnum().WithMessage("Tipo de evento inválido");
        RuleFor(c => c.TempoJogoSegundos).GreaterThanOrEqualTo(0).WithMessage("Tempo de jogo inválido");
    }
}
