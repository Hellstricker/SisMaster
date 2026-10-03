using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public class Campeonato : Entity
{
    public Guid AssociacaoId { get; private set; }
    public string Nome { get; private set; }
    public int Ano { get; private set; }
    public bool Ativo { get; private set; }

    public Associacao Associacao { get; private set; } = null!;

    protected Campeonato() { Nome = string.Empty; }

    public Campeonato(Guid associacaoId, string nome, int ano)
    {
        Validacoes.ValidarSeVazio(nome, "Nome do campeonato não pode ser vazio");
        Validacoes.ValidarMinimoMaximo(ano, 2000, 2100, "Ano inválido para o campeonato");

        AssociacaoId = associacaoId;
        Nome = nome;
        Ano = ano;
        Ativo = true;
    }
}
