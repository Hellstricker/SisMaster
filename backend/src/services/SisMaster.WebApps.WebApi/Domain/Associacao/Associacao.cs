using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public class Associacao : Entity, SisMaster.Core.DomainObjects.IAggregateRoot
{
    public string Nome { get; private set; }
    public string Sigla { get; private set; }
    public string Uf { get; private set; }
    public bool Ativa { get; private set; }

    private readonly List<Campeonato> _campeonatos = [];
    public IReadOnlyCollection<Campeonato> Campeonatos => _campeonatos.AsReadOnly();

    protected Associacao() { Nome = string.Empty; Sigla = string.Empty; Uf = string.Empty; }

    public Associacao(string nome, string sigla, string uf)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da associação não pode ser vazio");
        Validacoes.ValidarTamanho(nome, 3, 150, "Nome deve ter entre 3 e 150 caracteres");
        Validacoes.ValidarSeVazio(sigla, "Sigla não pode ser vazia");
        Validacoes.ValidarTamanho(sigla, 1, 10, "Sigla deve ter entre 1 e 10 caracteres");
        Validacoes.ValidarSeVazio(uf, "UF não pode ser vazia");
        Validacoes.ValidarTamanho(uf, 2, 2, "UF deve ter 2 caracteres");

        Nome = nome;
        Sigla = sigla.ToUpper();
        Uf = uf.ToUpper();
        Ativa = true;
    }

    public void Ativar() => Ativa = true;
    public void Desativar() => Ativa = false;

    public void Editar(string nome, string sigla, string uf)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da associação não pode ser vazio");
        Validacoes.ValidarTamanho(nome, 3, 150, "Nome deve ter entre 3 e 150 caracteres");
        Validacoes.ValidarSeVazio(sigla, "Sigla não pode ser vazia");
        Validacoes.ValidarTamanho(sigla, 1, 10, "Sigla deve ter entre 1 e 10 caracteres");
        Validacoes.ValidarSeVazio(uf, "UF não pode ser vazia");
        Validacoes.ValidarTamanho(uf, 2, 2, "UF deve ter 2 caracteres");

        Nome = nome;
        Sigla = sigla.ToUpper();
        Uf = uf.ToUpper();
    }

    public Campeonato AdicionarCampeonato(string nome)
    {
        var campeonato = new Campeonato(Id, nome);
        _campeonatos.Add(campeonato);
        return campeonato;
    }
}
