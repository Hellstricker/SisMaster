using FluentValidation;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Substitui os dados da súmula pelos de um jogo já realizado, importados do FIBA LiveStats, e encerra o jogo.</summary>
public class ImportarFibaCommand : Command
{
    public Guid SumulaId { get; set; }
    public string? Codigo { get; set; }
    public bool InverterLados { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ImportarFibaCommandValidation().Validate(this);
        return ValidationResult.IsValid;
    }
}

public class ImportarFibaCommandValidation : AbstractValidator<ImportarFibaCommand>
{
    public ImportarFibaCommandValidation()
    {
        RuleFor(c => c.SumulaId).NotEmpty().WithMessage("Id da súmula inválido");
        RuleFor(c => c.Codigo).NotEmpty().WithMessage("Informe o código do jogo no LiveStats (só números)");
    }
}
