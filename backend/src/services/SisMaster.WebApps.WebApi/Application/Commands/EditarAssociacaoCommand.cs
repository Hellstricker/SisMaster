using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class EditarAssociacaoCommand : Command
{
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Sigla { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new EditarAssociacaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class EditarAssociacaoCommandValidation : AbstractValidator<EditarAssociacaoCommand>
{
    public EditarAssociacaoCommandValidation()
    {
        RuleFor(c => c.AssociacaoId).NotEmpty().WithMessage("Id da associação inválido");
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150).WithMessage("Nome deve ter entre 3 e 150 caracteres");
        RuleFor(c => c.Sigla).NotEmpty().Length(1, 10).WithMessage("Sigla deve ter entre 1 e 10 caracteres");
        RuleFor(c => c.Uf).NotEmpty().Length(2, 2).WithMessage("UF deve ter exatamente 2 caracteres");
    }
}
