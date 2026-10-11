using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Reabre uma súmula encerrada para correção: o jogo volta a Em andamento. O motivo é obrigatório e fica no histórico do jogo.</summary>
public class ReabrirSumulaCommand : Command
{
    public Guid SumulaId { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public override bool EhValido()
    {
        ValidationResult = new ReabrirSumulaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ReabrirSumulaCommandValidation : AbstractValidator<ReabrirSumulaCommand>
{
    public ReabrirSumulaCommandValidation()
    {
        RuleFor(c => c.SumulaId).NotEmpty().WithMessage("Id da súmula inválido");
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Informe o motivo para reabrir a súmula")
            .MinimumLength(5).WithMessage("Descreva o motivo com pelo menos 5 caracteres")
            .MaximumLength(300).WithMessage("O motivo deve ter até 300 caracteres");
    }
}
