using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class AprovarInscricaoCategoriaCommand : Command
{
    public Guid InscricaoCategoriaId { get; set; }

    /// <summary>Obrigatória quando idade/sexo fogem do esperado para a categoria.</summary>
    public string? JustificativaExcecao { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new AprovarInscricaoCategoriaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class AprovarInscricaoCategoriaCommandValidation : AbstractValidator<AprovarInscricaoCategoriaCommand>
{
    public AprovarInscricaoCategoriaCommandValidation()
    {
        RuleFor(c => c.InscricaoCategoriaId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.JustificativaExcecao).MaximumLength(500).WithMessage("Justificativa deve ter até 500 caracteres");
    }
}
