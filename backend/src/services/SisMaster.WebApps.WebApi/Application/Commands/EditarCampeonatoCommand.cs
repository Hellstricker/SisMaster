using FluentValidation;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EditarCampeonatoCommand : Command
{
    public Guid CampeonatoId { get; set; }
    public string Nome { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new EditarCampeonatoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EditarCampeonatoCommandValidation : AbstractValidator<EditarCampeonatoCommand>
{
    public EditarCampeonatoCommandValidation()
    {
        RuleFor(c => c.CampeonatoId).NotEmpty().WithMessage("Id do campeonato inválido");
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150).WithMessage("Nome deve ter entre 3 e 150 caracteres");
    }
}
