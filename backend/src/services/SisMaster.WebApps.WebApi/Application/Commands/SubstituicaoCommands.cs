using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Registra uma substituição no mesário de estatísticas: sai um jogador em quadra, entra um do banco do mesmo time.</summary>
public class RegistrarSubstituicaoCommand : Command
{
    public Guid PartidaId { get; set; }
    /// <summary>Nulo quando o jogador só entra para completar a quadra (equipe que começou com menos de 5).</summary>
    public Guid? JogadorSaiId { get; set; }
    public Guid JogadorEntraId { get; set; }
    public int TempoJogoSegundos { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RegistrarSubstituicaoCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RegistrarSubstituicaoCommandValidation : AbstractValidator<RegistrarSubstituicaoCommand>
{
    public RegistrarSubstituicaoCommandValidation()
    {
        RuleFor(c => c.PartidaId).NotEmpty().WithMessage("PartidaId é obrigatório");
        RuleFor(c => c.JogadorEntraId).NotEmpty().WithMessage("Informe quem entra");
        RuleFor(c => c.TempoJogoSegundos).GreaterThanOrEqualTo(0).WithMessage("Tempo de jogo inválido");
    }
}

public class DesfazerUltimaSubstituicaoCommand : Command
{
    public Guid PartidaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ValidationResult();
        if (PartidaId == Guid.Empty) ValidationResult.Errors.Add(new ValidationFailure(nameof(PartidaId), "PartidaId é obrigatório"));
        return ValidationResult.IsValid;
    }
}
