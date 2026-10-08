using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

/// <summary>
/// Identidade permanente, criada (ou reconhecida por CPF) na primeira inscrição. Qualquer pessoa
/// pode se inscrever; o perfil vira Associado automaticamente quando uma inscrição é paga.
/// </summary>
public class Pessoa : Entity, IAggregateRoot
{
    public string Nome { get; private set; } = string.Empty;
    public Cpf Cpf { get; private set; } = null!;
    public DateOnly Nascimento { get; private set; }
    public Sexo Sexo { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public PerfilPessoa Perfil { get; private set; }

    protected Pessoa() { }

    public Pessoa(string nome, string cpf, DateOnly nascimento, Sexo sexo, string email, string? telefone = null)
    {
        Validar(nome, nascimento, email);

        Nome = nome.Trim();
        Cpf = new Cpf(Cpf.RemoverFormatacao(cpf));
        Nascimento = nascimento;
        Sexo = sexo;
        Email = email.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
        Perfil = PerfilPessoa.Convidado;
    }

    public void TornarAssociado() => Perfil = PerfilPessoa.Associado;

    private static void Validar(string nome, DateOnly nascimento, string email)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da pessoa não pode ser vazio");
        Validacoes.ValidarTamanho(nome, 3, 150, "Nome deve ter entre 3 e 150 caracteres");
        Validacoes.ValidarSeVerdadeiro(
            nascimento > DateOnly.FromDateTime(DateTime.Today) || nascimento.Year < 1900,
            "Data de nascimento inválida");
        Validacoes.ValidarSeVazio(email, "E-mail não pode ser vazio");
        Validacoes.ValidarTamanho(email, 5, 150, "E-mail deve ter entre 5 e 150 caracteres");
        Validacoes.ValidarSeVerdadeiro(!email.Contains('@'), "E-mail inválido");
    }
}
