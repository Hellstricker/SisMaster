using FluentValidation.Results;
using SisMaster.Core.Messages;

namespace SisMaster.WebApps.WebApi.Application.Commands;

/// <summary>
/// Registra o sorteio da diretoria para um empate sem critério numa tabela da fase (a fase inteira ou um grupo).
/// <see cref="Ordem"/>: ids das vagas empatadas, da melhor para a pior colocação.
/// </summary>
public class DefinirSorteioCommand : Command
{
    public Guid FaseId { get; set; }

    /// <summary>Grupo da tabela (1 = A, 2 = B…); nulo em pontos corridos.</summary>
    public int? GrupoOrdem { get; set; }
    public List<Guid> Ordem { get; set; } = [];

    public override bool EhValido()
    {
        ValidationResult = new ValidationResult();
        if (FaseId == Guid.Empty) ValidationResult.Errors.Add(new ValidationFailure(nameof(FaseId), "FaseId é obrigatório"));
        if (Ordem.Count < 2) ValidationResult.Errors.Add(new ValidationFailure(nameof(Ordem), "Informe a ordem sorteada das equipes empatadas"));
        return ValidationResult.IsValid;
    }
}
