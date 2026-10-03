using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Time.VOs;

namespace SisMaster.WebApps.WebApi.Domain.Time;

public class Time : Entity, SisMaster.Core.DomainObjects.IAggregateRoot
{
    public Guid AssociacaoId { get; private set; }
    public string Nome { get; private set; }
    public string Sigla { get; private set; }
    public bool Ativo { get; private set; }

    private readonly List<Jogador> _jogadores = [];
    public IReadOnlyCollection<Jogador> Jogadores => _jogadores.AsReadOnly();

    protected Time() { Nome = string.Empty; Sigla = string.Empty; }

    public Time(Guid associacaoId, string nome, string sigla)
    {
        Validacoes.ValidarSeVazio(nome, "Nome do time não pode ser vazio");
        Validacoes.ValidarTamanho(nome, 2, 100, "Nome do time deve ter entre 2 e 100 caracteres");
        Validacoes.ValidarSeVazio(sigla, "Sigla do time não pode ser vazia");
        Validacoes.ValidarTamanho(sigla, 2, 10, "Sigla deve ter entre 2 e 10 caracteres");

        AssociacaoId = associacaoId;
        Nome = nome;
        Sigla = sigla.ToUpper();
        Ativo = true;
    }

    public Jogador AdicionarJogador(int numero, Pessoa pessoa)
    {
        var jogadorExistente = _jogadores.FirstOrDefault(j => j.Numero == numero && j.Ativo);
        if (jogadorExistente is not null)
            throw new DomainException($"Já existe um jogador ativo com o número {numero} neste time");

        var jogador = new Jogador(Id, numero, pessoa);
        _jogadores.Add(jogador);
        return jogador;
    }
}
