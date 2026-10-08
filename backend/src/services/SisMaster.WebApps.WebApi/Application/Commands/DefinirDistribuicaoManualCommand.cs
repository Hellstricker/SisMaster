using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>Define o grupo (1 = A, 2 = B…) de cada vaga de uma fase de grupos com distribuição manual, na ordem das vagas.</summary>
public class DefinirDistribuicaoManualCommand : Command
{
    public Guid FaseId { get; set; }
    public List<int> Grupos { get; set; } = [];

    public override bool EhValido()
    {
        ValidationResult = new ValidationResult();
        if (FaseId == Guid.Empty) ValidationResult.Errors.Add(new ValidationFailure(nameof(FaseId), "FaseId é obrigatório"));
        if (Grupos.Count == 0) ValidationResult.Errors.Add(new ValidationFailure(nameof(Grupos), "Informe o grupo de cada equipe"));
        return ValidationResult.IsValid;
    }
}
