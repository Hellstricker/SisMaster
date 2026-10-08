using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

/// <summary>
/// Catálogo da associação. É só o modelo/padrão usado para pré-preencher o snapshot de
/// <see cref="TemporadaCategoria"/>; inscrição, equipe e classificação nunca o leem direto.
/// Sexo nulo = categoria mista (divisões por sexo são categorias distintas). Cada temporada pode
/// ajustar a elegibilidade no seu snapshot (<see cref="TemporadaCategoria.ConfigurarElegibilidade"/>).
/// </summary>
public class Categoria : Entity
{
    public Guid AssociacaoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int IdadeMinima { get; private set; }
    public Sexo? Sexo { get; private set; }

    /// <summary>Quando verdadeiro, atleta abaixo da idade mínima é aceito sem exceção.</summary>
    public bool AceitaAbaixoIdadeMinima { get; private set; }
    public int MinimoPeriodosEmQuadra { get; private set; }
    public int MinimoPeriodosForaQuadra { get; private set; }

    public Associacao Associacao { get; private set; } = null!;

    protected Categoria() { }

    public Categoria(Guid associacaoId, string nome, int idadeMinima = 0, Sexo? sexo = null,
        int minimoPeriodosEmQuadra = 1, int minimoPeriodosForaQuadra = 1, bool aceitaAbaixoIdadeMinima = false)
    {
        AssociacaoId = associacaoId;
        Definir(nome, idadeMinima, sexo, minimoPeriodosEmQuadra, minimoPeriodosForaQuadra);
        AceitaAbaixoIdadeMinima = aceitaAbaixoIdadeMinima;
    }

    public void Editar(string nome, int idadeMinima, Sexo? sexo, int minimoPeriodosEmQuadra, int minimoPeriodosForaQuadra, bool aceitaAbaixoIdadeMinima)
    {
        Definir(nome, idadeMinima, sexo, minimoPeriodosEmQuadra, minimoPeriodosForaQuadra);
        AceitaAbaixoIdadeMinima = aceitaAbaixoIdadeMinima;
    }

    private void Definir(string nome, int idadeMinima, Sexo? sexo, int minimoEmQuadra, int minimoForaQuadra)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da categoria é obrigatório");
        Validacoes.ValidarTamanho(nome, 1, 100, "Nome da categoria deve ter até 100 caracteres");
        Validacoes.ValidarMinimoMaximo(idadeMinima, 0, 120, "Idade mínima inválida");
        Validacoes.ValidarMinimoMaximo(minimoEmQuadra, 0, 4, "Mínimo de períodos em quadra deve estar entre 0 e 4");
        Validacoes.ValidarMinimoMaximo(minimoForaQuadra, 0, 4, "Mínimo de períodos fora da quadra deve estar entre 0 e 4");
        Validacoes.ValidarSeVerdadeiro(minimoEmQuadra + minimoForaQuadra > 4,
            "A soma dos mínimos de rodízio não pode passar de 4 períodos");

        Nome = nome;
        IdadeMinima = idadeMinima;
        Sexo = sexo;
        MinimoPeriodosEmQuadra = minimoEmQuadra;
        MinimoPeriodosForaQuadra = minimoForaQuadra;
    }
}
