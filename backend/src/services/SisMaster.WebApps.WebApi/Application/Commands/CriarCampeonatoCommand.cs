using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarCampeonatoCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new CriarCampeonatoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarCampeonatoCommandValidation : AbstractValidator<CriarCampeonatoCommand>
{
    public CriarCampeonatoCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("Id da associação inválido");
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150).WithMessage("Nome deve ter entre 3 e 150 caracteres");
    }
}
