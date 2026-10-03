using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public class Associacao : Entity, SisMaster.Core.DomainObjects.IAggregateRoot
{
    public string Nome { get; private set; }
    public string Uf { get; private set; }
    public bool Ativa { get; private set; }

    private readonly List<Campeonato> _campeonatos = [];
    public IReadOnlyCollection<Campeonato> Campeonatos => _campeonatos.AsReadOnly();

    protected Associacao() { Nome = string.Empty; Uf = string.Empty; }

    public Associacao(string nome, string uf)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da associação não pode ser vazio");
        Validacoes.ValidarTamanho(nome, 3, 150, "Nome deve ter entre 3 e 150 caracteres");
        Validacoes.ValidarSeVazio(uf, "UF não pode ser vazia");
        Validacoes.ValidarTamanho(uf, 2, 2, "UF deve ter 2 caracteres");

        Nome = nome;
        Uf = uf.ToUpper();
        Ativa = true;
    }

    public Campeonato AdicionarCampeonato(string nome, int ano)
    {
        var campeonato = new Campeonato(Id, nome, ano);
        _campeonatos.Add(campeonato);
        return campeonato;
    }
}
