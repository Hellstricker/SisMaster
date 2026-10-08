using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.DomainObjects;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Origem de uma equipe num confronto, como chega da API. Só os campos do <see cref="Tipo"/> são usados.</summary>
public class OrigemDto
{
    public TipoReferencia Tipo { get; set; }
    public Guid? EquipeId { get; set; }
    public Guid? FaseId { get; set; }
    public int? GrupoOrdem { get; set; }
    public int? Posicao { get; set; }
    public Guid? ConfrontoId { get; set; }

    public ReferenciaEquipe ParaReferencia() => Tipo switch
    {
        TipoReferencia.Equipe => ReferenciaEquipe.DeEquipe(EquipeId ?? throw new DomainException("Informe a equipe")),
        TipoReferencia.Colocacao => ReferenciaEquipe.DeColocacao(
            FaseId ?? throw new DomainException("Informe a fase da colocação"), GrupoOrdem,
            Posicao ?? throw new DomainException("Informe a posição")),
        TipoReferencia.Vencedor => ReferenciaEquipe.DeVencedor(ConfrontoId ?? throw new DomainException("Informe o confronto")),
        TipoReferencia.Perdedor => ReferenciaEquipe.DePerdedor(ConfrontoId ?? throw new DomainException("Informe o confronto")),
        _ => throw new DomainException("Tipo de origem inválido")
    };
}

/// <summary>Cria ou substitui o confronto de número <see cref="Numero"/> de uma fase de mata-mata (enquanto a tabela de jogos não foi gerada).</summary>
public class DefinirConfrontoCommand : Command
{
    public Guid FaseId { get; set; }
    public int Numero { get; set; }
    public string? Nome { get; set; }
    public OrigemDto OrigemA { get; set; } = new();
    public OrigemDto OrigemB { get; set; } = new();

    public override bool EhValido()
    {
        ValidationResult = new DefinirConfrontoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class DefinirConfrontoCommandValidation : AbstractValidator<DefinirConfrontoCommand>
{
    public DefinirConfrontoCommandValidation()
    {
        RuleFor(c => c.FaseId).NotEmpty().WithMessage("Id da fase inválido");
        RuleFor(c => c.Numero).InclusiveBetween(1, 16).WithMessage("Número do confronto inválido");
        RuleFor(c => c.Nome).MaximumLength(60).WithMessage("Nome do confronto deve ter até 60 caracteres");
        RuleFor(c => c.OrigemA).NotNull().WithMessage("Informe a origem da primeira equipe");
        RuleFor(c => c.OrigemB).NotNull().WithMessage("Informe a origem da segunda equipe");
    }
}
