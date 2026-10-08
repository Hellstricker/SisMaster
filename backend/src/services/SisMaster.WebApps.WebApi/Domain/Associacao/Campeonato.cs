using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public class Campeonato : Entity
{
    public Guid AssociacaoId { get; private set; }
    public string Nome { get; private set; }
    public bool Ativo { get; private set; }

    public Associacao Associacao { get; private set; } = null!;

    private readonly List<Temporada> _temporadas = [];
    public IReadOnlyCollection<Temporada> Temporadas => _temporadas.AsReadOnly();

    protected Campeonato() { Nome = string.Empty; }

    public Campeonato(Guid associacaoId, string nome)
    {
        Validacoes.ValidarSeVazio(nome, "Nome do campeonato não pode ser vazio");

        AssociacaoId = associacaoId;
        Nome = nome;
        Ativo = true;
    }

    public void Renomear(string nome)
    {
        Validacoes.ValidarSeVazio(nome, "Nome do campeonato não pode ser vazio");
        Validacoes.ValidarTamanho(nome.Trim(), 3, 150, "Nome deve ter entre 3 e 150 caracteres");
        Nome = nome.Trim();
    }

    public Temporada AdicionarTemporada(int ano, DateTime dataInicioInscricoes, DateTime dataFimInscricoes)
    {
        var temporada = new Temporada(Id, ano, dataInicioInscricoes, dataFimInscricoes);
        _temporadas.Add(temporada);
        return temporada;
    }

    public Temporada? ObterTemporada(Guid temporadaId) =>
        _temporadas.FirstOrDefault(t => t.Id == temporadaId);
}
