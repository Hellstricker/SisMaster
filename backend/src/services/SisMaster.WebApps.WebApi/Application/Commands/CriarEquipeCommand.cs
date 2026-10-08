using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarEquipeCommand : Command
{
    public Guid TemporadaId { get; set; }
    public Guid TemporadaCategoriaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cor { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new CriarEquipeCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarEquipeCommandValidation : AbstractValidator<CriarEquipeCommand>
{
    public CriarEquipeCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.TemporadaCategoriaId).NotEmpty().WithMessage("Categoria é obrigatória");
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100).WithMessage("Nome deve ter entre 1 e 100 caracteres");
    }
}
