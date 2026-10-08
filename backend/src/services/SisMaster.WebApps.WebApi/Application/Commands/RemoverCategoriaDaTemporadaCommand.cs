using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

public class RemoverCategoriaDaTemporadaCommand : Command
{
    public Guid TemporadaId { get; set; }
    public Guid CategoriaId { get; set; }

    public override bool EhValido()
    {
        ValidationResult = new ValidationResult();
        if (TemporadaId == Guid.Empty)
            ValidationResult.Errors.Add(new ValidationFailure("TemporadaId", "TemporadaId é obrigatório"));
        if (CategoriaId == Guid.Empty)
            ValidationResult.Errors.Add(new ValidationFailure("CategoriaId", "CategoriaId é obrigatório"));
        return ValidationResult.IsValid;
    }
}
