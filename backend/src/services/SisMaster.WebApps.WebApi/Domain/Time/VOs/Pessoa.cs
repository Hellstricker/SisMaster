using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Time.VOs;

public class Pessoa
{
    public string Nome { get; private set; }
    public Cpf Cpf { get; private set; }

    protected Pessoa()
    {
        Nome = string.Empty;
        Cpf = null!;
    }

    public Pessoa(string nome, string cpf)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da pessoa não pode ser vazio");
        Validacoes.ValidarTamanho(nome, 3, 150, "Nome deve ter entre 3 e 150 caracteres");

        Nome = nome;
        Cpf = new Cpf(Cpf.RemoverFormatacao(cpf));
    }
}
