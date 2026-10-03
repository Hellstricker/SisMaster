using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarPartidaCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public Guid TimeCasaId { get; set; }
    public Guid TimeVisitanteId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new CriarPartidaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarPartidaCommandValidation : AbstractValidator<CriarPartidaCommand>
{
    public CriarPartidaCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("CampeonatoId é obrigatório");
        RuleFor(c => c.TimeCasaId).NotEmpty().WithMessage("TimeCasaId é obrigatório");
        RuleFor(c => c.TimeVisitanteId).NotEmpty().WithMessage("TimeVisitanteId é obrigatório");
        RuleFor(c => c).Must(c => c.TimeCasaId != c.TimeVisitanteId)
            .WithMessage("Casa e visitante não podem ser o mesmo time");
    }
}
