using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class CriarFaseCommand : Command
{
    public Guid TemporadaId { get; set; }
    public Guid TemporadaCategoriaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoFase Tipo { get; set; }

    /// <summary>Pontos corridos e grupos (1 a 3). Ignorado no mata-mata.</summary>
    public int? NumeroTurnos { get; set; }

    /// <summary>Grupos: de 2 a 8. Ignorado nos demais tipos.</summary>
    public int? NumeroGrupos { get; set; }

    /// <summary>Grupos: como as equipes entram em cada grupo. Ignorado nos demais tipos.</summary>
    public DistribuicaoEquipes? Distribuicao { get; set; }

    /// <summary>Mata-mata: melhor de 1, 3, 5 ou 7. Ignorado nos demais tipos.</summary>
    public int? JogosPorConfronto { get; set; }

    /// <summary>Mata-mata: quantos confrontos (1 a 16). Ignorado nos demais tipos.</summary>
    public int? NumeroConfrontos { get; set; }

    /// <summary>Pontos corridos/grupos: quantos se classificam (por grupo, em Grupos). Nulo = todos.</summary>
    public int? ClassificadosPrimeiros { get; set; }

    /// <summary>Só em Grupos: melhores (N+1)º colocados que também se classificam.</summary>
    public int MelhoresExtras { get; set; }

    public EstruturaFase Estrutura() =>
        new(NumeroTurnos, NumeroGrupos, Distribuicao, JogosPorConfronto, NumeroConfrontos, ClassificadosPrimeiros, MelhoresExtras);

    /// <summary>De onde vêm os classificados (opcional).</summary>
    public Guid? FaseAnteriorId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new CriarFaseCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class CriarFaseCommandValidation : AbstractValidator<CriarFaseCommand>
{
    public CriarFaseCommandValidation()
    {
        RuleFor(c => c.TemporadaId).NotEmpty().WithMessage("Id da temporada inválido");
        RuleFor(c => c.TemporadaCategoriaId).NotEmpty().WithMessage("Categoria é obrigatória");
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100).WithMessage("Nome deve ter entre 1 e 100 caracteres");
    }
}
