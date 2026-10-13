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

    /// <summary>Id da conta do site (Identidade) ligada a este cadastro. Uma conta tem um cadastro e um cadastro tem uma conta.</summary>
    public string? ContaId { get; private set; }

    /// <summary>
    /// E-mail da conta do site que a diretoria autorizou a ligar a este cadastro quando o e-mail do cadastro é outro. A conta continua
    /// provando o e-mail pelo token (confirmado); o CPF digitado também precisa bater. Some depois que a ligação é feita.
    /// </summary>
    public string? EmailAutorizadoParaLigacao { get; private set; }

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

    /// <summary>Liga o cadastro à conta do site. Nunca troca uma ligação existente (isso é com a diretoria).</summary>
    public void VincularConta(string contaId)
    {
        Validacoes.ValidarSeVazio(contaId, "Conta inválida");
        if (ContaId is not null && ContaId != contaId)
            throw new DomainException("Este cadastro já está ligado a outra conta");
        ContaId = contaId;
        EmailAutorizadoParaLigacao = null;
    }

    /// <summary>Solta a conta do cadastro (usado ao unir duplicidades, antes de a conta passar ao cadastro principal).</summary>
    public string? DesligarConta()
    {
        var anterior = ContaId;
        ContaId = null;
        return anterior;
    }

    /// <summary>A diretoria autoriza (ou, com e-mail vazio, cancela a autorização de) a ligação com a conta do site que tenha este e-mail.</summary>
    public void AutorizarLigacaoComConta(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) { EmailAutorizadoParaLigacao = null; return; }
        Validacoes.ValidarSeVerdadeiro(ContaId is not null, "Este cadastro já está ligado a uma conta do site");
        Validacoes.ValidarTamanho(email.Trim(), 5, 150, "E-mail deve ter entre 5 e 150 caracteres");
        Validacoes.ValidarSeVerdadeiro(!email.Contains('@'), "E-mail inválido");
        EmailAutorizadoParaLigacao = email.Trim();
    }

    /// <summary>A conta com este e-mail pode se ligar a este cadastro? (e-mail do cadastro igual ao da conta, ou ligação autorizada pela diretoria)</summary>
    public bool AceitaLigacaoDaConta(string emailDaConta) =>
        string.Equals(Email, emailDaConta, StringComparison.OrdinalIgnoreCase)
        || (EmailAutorizadoParaLigacao is not null && string.Equals(EmailAutorizadoParaLigacao, emailDaConta, StringComparison.OrdinalIgnoreCase));

    /// <summary>Menor de 18 anos na data: a inscrição é feita pela diretoria, com o responsável.</summary>
    public static bool EhMenorDeIdade(DateOnly nascimento, DateOnly hoje) => nascimento.AddYears(18) > hoje;

    public void TornarAssociado() => Perfil = PerfilPessoa.Associado;

    /// <summary>Corrige e-mail e telefone (não afetam a elegibilidade).</summary>
    public void AtualizarContato(string email, string? telefone)
    {
        Validacoes.ValidarSeVazio(email, "E-mail não pode ser vazio");
        Validacoes.ValidarTamanho(email, 5, 150, "E-mail deve ter entre 5 e 150 caracteres");
        Validacoes.ValidarSeVerdadeiro(!email.Contains('@'), "E-mail inválido");
        Email = email.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
    }

    /// <summary>Corrige os dados que definem a elegibilidade nas categorias. Só o Administrador, com registro de auditoria.</summary>
    public void CorrigirIdentificacao(string nome, string cpf, DateOnly nascimento, Sexo sexo)
    {
        Validar(nome, nascimento, Email);
        Nome = nome.Trim();
        Cpf = new Cpf(Cpf.RemoverFormatacao(cpf));
        Nascimento = nascimento;
        Sexo = sexo;
    }

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
