using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarTimeCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Sigla { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new CriarTimeCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarTimeCommandValidation : AbstractValidator<CriarTimeCommand>
{
    public CriarTimeCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("AssociacaoId é obrigatório");
        RuleFor(c => c.Nome).NotEmpty().Length(2, 100).WithMessage("Nome deve ter entre 2 e 100 caracteres");
        RuleFor(c => c.Sigla).NotEmpty().Length(2, 10).WithMessage("Sigla deve ter entre 2 e 10 caracteres");
    }
}
