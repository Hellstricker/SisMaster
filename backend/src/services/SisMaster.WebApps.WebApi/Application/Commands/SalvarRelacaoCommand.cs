using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RelacionadoDto
{
    public Guid AtletaId { get; set; }

    /// <summary>"0", "00" ou "1" a "99".</summary>
    public string Numero { get; set; } = string.Empty;
    public bool Titular { get; set; }
}

/// <summary>
/// Salva (substituindo) a relação de um dos times da súmula: técnico, auxiliar, capitão e os jogadores com a camisa do jogo.
/// Pode ficar incompleta; só iniciar a súmula exige tudo.
/// </summary>
public class SalvarRelacaoCommand : Command
{
    public Guid JogoId { get; set; }
    public LadoTime Lado { get; set; }
    public string? Tecnico { get; set; }
    public string? AuxiliarTecnico { get; set; }
    public Guid? CapitaoAtletaId { get; set; }
    public List<RelacionadoDto> Jogadores { get; set; } = [];

    public override bool EhValido()
    {
        ValidationResult = new SalvarRelacaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class SalvarRelacaoCommandValidation : AbstractValidator<SalvarRelacaoCommand>
{
    public SalvarRelacaoCommandValidation()
    {
        RuleFor(c => c.JogoId).NotEmpty().WithMessage("Id do jogo inválido");
        RuleFor(c => c.Lado).IsInEnum().WithMessage("Lado inválido");
        RuleFor(c => c.Tecnico).MaximumLength(100).WithMessage("O nome do técnico deve ter até 100 caracteres");
        RuleFor(c => c.AuxiliarTecnico).MaximumLength(100).WithMessage("O nome do auxiliar deve ter até 100 caracteres");
        RuleFor(c => c.Jogadores).NotNull().WithMessage("Informe os jogadores");
    }
}
