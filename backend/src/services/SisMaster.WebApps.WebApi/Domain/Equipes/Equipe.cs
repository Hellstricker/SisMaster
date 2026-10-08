using System.Text.RegularExpressions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Domain.Equipes;

/// <summary>
/// Equipe de uma categoria da temporada. O elenco é o conjunto de <see cref="Atleta"/> (vínculos com pedidos
/// de inscrição efetivados), sem tabela própria de "elenco". O nome é único na temporada (entre categorias).
/// </summary>
public class Equipe : Entity, IAggregateRoot
{
    private static readonly Regex CorHex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public Guid TemporadaCategoriaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Cor { get; private set; }

    public TemporadaCategoria TemporadaCategoria { get; private set; } = null!;

    private readonly List<Atleta> _atletas = [];
    public IReadOnlyCollection<Atleta> Atletas => _atletas.AsReadOnly();

    protected Equipe() { }

    public Equipe(Guid temporadaCategoriaId, string nome, string? cor)
    {
        TemporadaCategoriaId = temporadaCategoriaId;
        Definir(nome, cor);
    }

    public void Editar(string nome, string? cor) => Definir(nome, cor);

    private void Definir(string nome, string? cor)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da equipe é obrigatório");
        Validacoes.ValidarTamanho(nome.Trim(), 1, 100, "Nome da equipe deve ter até 100 caracteres");
        if (!string.IsNullOrWhiteSpace(cor))
            Validacoes.ValidarSeFalso(CorHex.IsMatch(cor), "Cor inválida (use o formato #RRGGBB)");

        Nome = nome.Trim();
        Cor = string.IsNullOrWhiteSpace(cor) ? null : cor.ToLowerInvariant();
    }

    public Atleta AdicionarAtleta(Guid inscricaoCategoriaId)
    {
        if (_atletas.Any(a => a.InscricaoCategoriaId == inscricaoCategoriaId))
            throw new DomainException("Este atleta já está no elenco da equipe");

        var atleta = new Atleta(Id, inscricaoCategoriaId);
        _atletas.Add(atleta);
        return atleta;
    }

    public Atleta RemoverAtleta(Guid atletaId)
    {
        var atleta = _atletas.FirstOrDefault(a => a.Id == atletaId)
            ?? throw new DomainException("Atleta não encontrado nesta equipe");
        _atletas.Remove(atleta);
        return atleta;
    }
}
