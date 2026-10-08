using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

/// <summary>
/// Categoria participante de uma temporada. Nome, idade mínima, sexo e mínimos de rodízio são
/// copiados de <see cref="Categoria"/> na criação e ficam congelados — mudar o padrão da
/// associação não reescreve temporadas passadas. Inscrição, equipe e fase referenciam este
/// vínculo, nunca a Categoria direto.
/// </summary>
public class TemporadaCategoria : Entity
{
    public Guid TemporadaId { get; private set; }
    public Guid CategoriaId { get; private set; }

    public string Nome { get; private set; } = string.Empty;
    public int IdadeMinima { get; private set; }
    public Sexo? Sexo { get; private set; }

    /// <summary>Quando verdadeiro, idade abaixo do mínimo não exige justificativa de exceção.</summary>
    public bool AceitaAbaixoIdadeMinima { get; private set; }
    public int MinimoPeriodosEmQuadra { get; private set; }
    public int MinimoPeriodosForaQuadra { get; private set; }

    /// <summary>Valor cobrado por inscrição. Nulo = "a definir".</summary>
    public decimal? Valor { get; private set; }

    public Temporada Temporada { get; private set; } = null!;
    public Categoria Categoria { get; private set; } = null!;

    protected TemporadaCategoria() { }

    public TemporadaCategoria(Guid temporadaId, Categoria categoria)
    {
        TemporadaId = temporadaId;
        CategoriaId = categoria.Id;
        Nome = categoria.Nome;
        IdadeMinima = categoria.IdadeMinima;
        Sexo = categoria.Sexo;
        AceitaAbaixoIdadeMinima = categoria.AceitaAbaixoIdadeMinima;
        MinimoPeriodosEmQuadra = categoria.MinimoPeriodosEmQuadra;
        MinimoPeriodosForaQuadra = categoria.MinimoPeriodosForaQuadra;
    }

    /// <summary>
    /// Motivos pelos quais a pessoa foge do esperado para a categoria (idade abaixo do mínimo ou
    /// sexo diferente do exigido). Idade = ano da temporada − ano de nascimento. Vazio = dentro do esperado.
    /// </summary>
    public IReadOnlyList<string> DescreverExcecoes(DateOnly nascimento, Sexo sexoPessoa, int anoTemporada)
    {
        var motivos = new List<string>();
        var idade = anoTemporada - nascimento.Year;
        if (!AceitaAbaixoIdadeMinima && idade < IdadeMinima)
            motivos.Add($"{Nome}: {idade} anos (mínimo {IdadeMinima})");
        if (Sexo is not null && sexoPessoa != Sexo)
            motivos.Add($"{Nome}: sexo diferente do exigido pela categoria");
        return motivos;
    }

    /// <summary>
    /// Ajusta a elegibilidade só desta temporada (categoria mista = sexo nulo; algumas edições aceitam
    /// atletas abaixo da idade mínima). Vale para as próximas aprovações; não altera a Categoria da associação.
    /// </summary>
    public void ConfigurarElegibilidade(int idadeMinima, Sexo? sexo, bool aceitaAbaixoIdadeMinima)
    {
        Validacoes.ValidarMinimoMaximo(idadeMinima, 0, 120, "Idade mínima inválida");
        IdadeMinima = idadeMinima;
        Sexo = sexo;
        AceitaAbaixoIdadeMinima = aceitaAbaixoIdadeMinima;
    }

    public void DefinirValor(decimal? valor)
    {
        if (valor is < 0)
            throw new DomainException("Valor da inscrição não pode ser negativo");
        Valor = valor;
    }
}
