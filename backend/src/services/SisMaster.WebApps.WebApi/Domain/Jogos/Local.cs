using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>Ginásio/quadra onde os jogos acontecem. Cadastrado uma vez por associação e reaproveitado entre jogos (inclusive rodadas em outras cidades).</summary>
public class Local : Entity, IAggregateRoot
{
    public Guid AssociacaoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Cidade { get; private set; } = string.Empty;
    public string? Estado { get; private set; }

    protected Local() { }

    public Local(Guid associacaoId, string nome, string cidade, string? estado)
    {
        AssociacaoId = associacaoId;
        Definir(nome, cidade, estado);
    }

    public void Editar(string nome, string cidade, string? estado) => Definir(nome, cidade, estado);

    private void Definir(string nome, string cidade, string? estado)
    {
        Validacoes.ValidarSeVazio(nome, "Nome do local é obrigatório");
        Validacoes.ValidarTamanho(nome.Trim(), 1, 100, "Nome do local deve ter até 100 caracteres");
        Validacoes.ValidarSeVazio(cidade, "Cidade é obrigatória");
        Validacoes.ValidarTamanho(cidade.Trim(), 1, 100, "Cidade deve ter até 100 caracteres");
        if (!string.IsNullOrWhiteSpace(estado))
            Validacoes.ValidarSeFalso(estado.Trim().Length == 2, "Estado deve ter 2 letras (UF)");

        Nome = nome.Trim();
        Cidade = cidade.Trim();
        Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
    }
}
