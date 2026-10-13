using FluentValidation;
using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RecusarInscricaoCategoriaCommand : Command
{
    public Guid InscricaoCategoriaId { get; set; }
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Avisar a pessoa por e-mail mesmo que a ficha seja da diretoria (quem se inscreveu pelo site é sempre avisado).</summary>
    public bool AvisarPorEmail { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new RecusarInscricaoCategoriaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class RecusarInscricaoCategoriaCommandValidation : AbstractValidator<RecusarInscricaoCategoriaCommand>
{
    public RecusarInscricaoCategoriaCommandValidation()
    {
        RuleFor(c => c.InscricaoCategoriaId).NotEmpty().WithMessage("Id da inscrição inválido");
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500).WithMessage("Informe o motivo da recusa (até 500 caracteres)");
    }
}
