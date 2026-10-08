using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Define o desconto de quem joga exatamente esta combinação de categorias (ao menos 2).</summary>
public class DefinirDescontoCommand : Command
{
    public Guid TemporadaId { get; set; }

    /// <summary>Ids das categorias da temporada (TemporadaCategoria) que formam a combinação.</summary>
    public List<Guid> TemporadaCategoriaIds { get; set; } = [];
    public TipoDesconto Tipo { get; set; }
    public decimal Valor { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new DefinirDescontoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class DefinirDescontoCommandValidation : AbstractValidator<DefinirDescontoCommand>
{
    public DefinirDescontoCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.TemporadaCategoriaIds).Must(l => l.Distinct().Count() >= 2).WithMessage("Escolha ao menos 2 categorias para o desconto");
        RuleFor(c => c.Valor).GreaterThanOrEqualTo(0).WithMessage("Valor do desconto não pode ser negativo");
        RuleFor(c => c.Valor).LessThanOrEqualTo(100).When(c => c.Tipo == TipoDesconto.Percentual).WithMessage("Percentual deve estar entre 0 e 100");
    }
}

public class RemoverDescontoCommand : Command
{
    public Guid TemporadaId { get; set; }
    public Guid DescontoId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ValidationResult();
        if (TemporadaId == Guid.Empty) ValidationResult.Errors.Add(new ValidationFailure(nameof(TemporadaId), "Id da temporada inválido"));
        if (DescontoId == Guid.Empty) ValidationResult.Errors.Add(new ValidationFailure(nameof(DescontoId), "Id do desconto inválido"));
        return ValidationResult.IsValid;
    }
}
