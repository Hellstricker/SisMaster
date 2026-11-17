using System.Security.Cryptography;
using SisMaster.Core.Data;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Domain.Torneio;

public enum StatusInscricaoDeEquipe
{
    /// <summary>Enviada: os atletas confirmam e a diretoria analisa.</summary>
    Pendente = 0,
    Aprovada = 1,
    Recusada = 2
}

public enum StatusDoConvite
{
    Enviado = 0,
    Confirmado = 1,
    Recusou = 2,
    /// <summary>Tirado do elenco pelo responsável ou pela recusa da equipe.</summary>
    Removido = 3
}

/// <summary>
/// A inscrição de uma equipe em uma categoria de um torneio aberto, feita pelo responsável: o nome da equipe, o elenco (um convite por atleta)
/// e a análise da diretoria. Cada atleta confirma o próprio convite (dados e autorizações) e passa a ter uma ficha de inscrição normal; a
/// taxa é por atleta e, ao ser registrada, o atleta fica efetivado e entra no elenco da <c>Equipe</c> criada na aprovação.
/// </summary>
public class InscricaoDeEquipe : Entity, IAggregateRoot
{
    public const int MinimoDeAtletas = 5;
    public const int MaximoDeAtletas = 15;
    public const int DiasParaConfirmar = 14;

    public Guid TemporadaId { get; private set; }
    public Guid TemporadaCategoriaId { get; private set; }

    /// <summary>Conta do site do responsável (Identidade).</summary>
    public string ContaId { get; private set; } = string.Empty;
    public string ResponsavelNome { get; private set; } = string.Empty;
    public string ResponsavelEmail { get; private set; } = string.Empty;
    public string? ResponsavelTelefone { get; private set; }

    public string Nome { get; private set; } = string.Empty;
    public string Protocolo { get; private set; } = string.Empty;
    public Guid CodigoDeEnvio { get; private set; }
    public DateTime DataEnvio { get; private set; }

    public StatusInscricaoDeEquipe Status { get; private set; }
    public string? MotivoRecusa { get; private set; }
    public DateTime? DecididaEm { get; private set; }
    public string? JustificativaExcecao { get; private set; }

    /// <summary>A equipe criada na aprovação (nula até lá).</summary>
    public Guid? EquipeId { get; private set; }

    private readonly List<ConviteDeAtleta> _convites = [];
    public IReadOnlyCollection<ConviteDeAtleta> Convites => _convites.AsReadOnly();

    protected InscricaoDeEquipe() { }

    public InscricaoDeEquipe(Guid temporadaId, Guid temporadaCategoriaId, string contaId, string responsavelNome, string responsavelEmail,
        string? responsavelTelefone, string nome, string protocolo, Guid codigoDeEnvio, DateTime dataEnvio)
    {
        Validacoes.ValidarSeVazio(contaId, "Conta inválida");
        Validacoes.ValidarSeVazio(responsavelNome, "Informe o nome do responsável");
        Validacoes.ValidarSeVazio(nome, "Informe o nome da equipe");
        Validacoes.ValidarTamanho(nome.Trim(), 3, 100, "O nome da equipe deve ter de 3 a 100 caracteres");
        Validacoes.ValidarSeVazio(protocolo, "Protocolo inválido");
        Validacoes.ValidarSeVerdadeiro(codigoDeEnvio == Guid.Empty, "Código de envio inválido");

        TemporadaId = temporadaId;
        TemporadaCategoriaId = temporadaCategoriaId;
        ContaId = contaId;
        ResponsavelNome = responsavelNome.Trim();
        ResponsavelEmail = responsavelEmail.Trim();
        ResponsavelTelefone = string.IsNullOrWhiteSpace(responsavelTelefone) ? null : responsavelTelefone.Trim();
        Nome = nome.Trim();
        Protocolo = protocolo;
        CodigoDeEnvio = codigoDeEnvio;
        DataEnvio = dataEnvio;
        Status = StatusInscricaoDeEquipe.Pendente;
    }

    public IEnumerable<ConviteDeAtleta> Ativos => _convites.Where(c => c.Status is StatusDoConvite.Enviado or StatusDoConvite.Confirmado);
    public int Confirmados => _convites.Count(c => c.Status == StatusDoConvite.Confirmado);
    public int ConvitesEmAberto => _convites.Count(c => c.Status == StatusDoConvite.Enviado);

    /// <summary>"Aguardando confirmações" enquanto faltam atletas confirmados ou há convite sem resposta; "Em análise" quando só falta a diretoria.</summary>
    public bool AguardandoConfirmacoes => Status == StatusInscricaoDeEquipe.Pendente && (Confirmados < MinimoDeAtletas || ConvitesEmAberto > 0);

    /// <summary>Cria o convite e devolve o código secreto (só vai por e-mail; o banco guarda apenas o hash).</summary>
    public (ConviteDeAtleta Convite, string Codigo) Convidar(string nome, string cpfSomenteDigitos, DateOnly nascimento, Sexo sexo, string email, DateTime agora, bool ehResponsavel = false)
    {
        Validacoes.ValidarSeVerdadeiro(Status != StatusInscricaoDeEquipe.Pendente && Status != StatusInscricaoDeEquipe.Aprovada, "Esta inscrição de equipe não aceita mais atletas");
        Validacoes.ValidarSeVerdadeiro(Ativos.Count() >= MaximoDeAtletas, $"O elenco pode ter no máximo {MaximoDeAtletas} atletas");
        Validacoes.ValidarSeVerdadeiro(Ativos.Any(c => c.Cpf == cpfSomenteDigitos), "Este CPF já está no elenco");
        Validacoes.ValidarSeVerdadeiro(Ativos.Any(c => string.Equals(c.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)), "Este e-mail já está no elenco: cada atleta precisa do próprio e-mail");

        var codigo = NovoCodigo();
        var convite = new ConviteDeAtleta(Id, nome, cpfSomenteDigitos, nascimento, sexo, email, Hash(codigo), agora.AddDays(DiasParaConfirmar), ehResponsavel);
        _convites.Add(convite);
        return (convite, codigo);
    }

    /// <summary>Novo código para o convite (o anterior deixa de valer) e novo prazo.</summary>
    public string Reenviar(Guid conviteId, DateTime agora)
    {
        var convite = Obter(conviteId);
        Validacoes.ValidarSeVerdadeiro(convite.Status != StatusDoConvite.Enviado, "Só dá para reenviar um convite que ainda não foi respondido");
        var codigo = NovoCodigo();
        convite.RenovarCodigo(Hash(codigo), agora.AddDays(DiasParaConfirmar));
        return codigo;
    }

    /// <summary>O responsável tira do elenco quem ainda não confirmou (ou quem recusou). Quem confirmou só sai por decisão da diretoria.</summary>
    public void Remover(Guid conviteId)
    {
        var convite = Obter(conviteId);
        Validacoes.ValidarSeVerdadeiro(convite.Status == StatusDoConvite.Confirmado, "Quem já confirmou a participação não pode ser removido pelo responsável: fale com a diretoria");
        convite.Remover();
    }

    public void Aprovar(Guid equipeId, string? justificativaExcecao, DateTime agora)
    {
        Validacoes.ValidarSeVerdadeiro(Status != StatusInscricaoDeEquipe.Pendente, "Só é possível aprovar uma equipe pendente");
        Validacoes.ValidarSeVerdadeiro(Confirmados < MinimoDeAtletas, $"A equipe precisa de pelo menos {MinimoDeAtletas} atletas confirmados para ser aprovada");
        Status = StatusInscricaoDeEquipe.Aprovada;
        EquipeId = equipeId;
        DecididaEm = agora;
        JustificativaExcecao = string.IsNullOrWhiteSpace(justificativaExcecao) ? null : justificativaExcecao.Trim();
    }

    public void Recusar(string motivo, DateTime agora)
    {
        Validacoes.ValidarSeVerdadeiro(Status != StatusInscricaoDeEquipe.Pendente, "Só é possível recusar uma equipe pendente");
        Validacoes.ValidarSeVazio(motivo, "Informe o motivo da recusa");
        Validacoes.ValidarSeVerdadeiro(motivo.Trim().Length > 500, "O motivo deve ter até 500 caracteres");
        Status = StatusInscricaoDeEquipe.Recusada;
        MotivoRecusa = motivo.Trim();
        DecididaEm = agora;
        foreach (var c in _convites.Where(c => c.Status == StatusDoConvite.Enviado).ToList()) c.Remover();
    }

    public ConviteDeAtleta Obter(Guid conviteId) =>
        _convites.FirstOrDefault(c => c.Id == conviteId) ?? throw new DomainException("Atleta não encontrado nesta equipe");

    public static string Hash(string codigo) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(codigo)));

    private static string NovoCodigo() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Um atleta convidado para a equipe. Os dados do atleta (CPF, nascimento) são apagados quando ele recusa ou é removido.</summary>
public class ConviteDeAtleta : Entity
{
    public Guid InscricaoDeEquipeId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Cpf { get; private set; }
    public DateOnly? Nascimento { get; private set; }
    public Sexo Sexo { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public bool EhResponsavel { get; private set; }

    /// <summary>SHA-256 do código enviado por e-mail (o código em si nunca é guardado).</summary>
    public string CodigoHash { get; private set; } = string.Empty;
    public DateTime ValidoAte { get; private set; }

    public StatusDoConvite Status { get; private set; }
    public DateTime? RespondidoEm { get; private set; }

    /// <summary>A ficha de inscrição e o cadastro do atleta, criados quando ele confirma.</summary>
    public Guid? InscricaoId { get; private set; }
    public Guid? PessoaId { get; private set; }

    protected ConviteDeAtleta() { }

    internal ConviteDeAtleta(Guid inscricaoDeEquipeId, string nome, string cpf, DateOnly nascimento, Sexo sexo, string email, string codigoHash, DateTime validoAte, bool ehResponsavel)
    {
        Validacoes.ValidarSeVazio(nome, "Informe o nome do atleta");
        Validacoes.ValidarTamanho(nome.Trim(), 3, 150, "O nome do atleta deve ter de 3 a 150 caracteres");
        Validacoes.ValidarSeVazio(email, "Informe o e-mail do atleta");
        Validacoes.ValidarSeVerdadeiro(!email.Contains('@') || email.Trim().Length > 150, "E-mail do atleta inválido");
        InscricaoDeEquipeId = inscricaoDeEquipeId;
        Nome = nome.Trim();
        Cpf = cpf;
        Nascimento = nascimento;
        Sexo = sexo;
        Email = email.Trim();
        EhResponsavel = ehResponsavel;
        CodigoHash = codigoHash;
        ValidoAte = validoAte;
        Status = StatusDoConvite.Enviado;
    }

    public bool Vencido(DateTime agora) => agora > ValidoAte;

    internal void RenovarCodigo(string codigoHash, DateTime validoAte) { CodigoHash = codigoHash; ValidoAte = validoAte; }

    public void Confirmar(Guid inscricaoId, Guid pessoaId, DateTime agora)
    {
        Validacoes.ValidarSeVerdadeiro(Status != StatusDoConvite.Enviado, "Este convite já foi respondido");
        Status = StatusDoConvite.Confirmado;
        InscricaoId = inscricaoId;
        PessoaId = pessoaId;
        RespondidoEm = agora;
    }

    public void Recusar(DateTime agora)
    {
        Validacoes.ValidarSeVerdadeiro(Status != StatusDoConvite.Enviado, "Este convite já foi respondido");
        Status = StatusDoConvite.Recusou;
        RespondidoEm = agora;
        ApagarDados();
    }

    internal void Remover()
    {
        Status = StatusDoConvite.Removido;
        ApagarDados();
    }

    /// <summary>Quem não participa não deixa CPF, nascimento nem e-mail guardados: sobra só o primeiro nome, para o responsável saber quem foi.</summary>
    private void ApagarDados()
    {
        Cpf = null;
        Nascimento = null;
        Email = "(removido)";
        Nome = Nome.Split(' ')[0];
        CodigoHash = string.Empty;
    }
}

public interface IInscricaoDeEquipeRepository : IRepository<InscricaoDeEquipe>
{
    /// <summary>A inscrição com os convites (rastreada).</summary>
    Task<InscricaoDeEquipe?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<InscricaoDeEquipe?> ObterPorCodigoDeEnvioAsync(Guid codigo, CancellationToken ct = default);

    /// <summary>A inscrição que contém o convite com este código (rastreada, com os convites).</summary>
    Task<InscricaoDeEquipe?> ObterPorCodigoDoConviteAsync(string codigo, CancellationToken ct = default);

    /// <summary>A inscrição de equipe de que a ficha de atleta faz parte (rastreada, com os convites).</summary>
    Task<InscricaoDeEquipe?> ObterPorFichaAsync(Guid inscricaoId, CancellationToken ct = default);

    /// <summary>As inscrições de equipe da conta, da mais recente à mais antiga (somente leitura).</summary>
    Task<IReadOnlyList<InscricaoDeEquipe>> ListarDaContaAsync(string contaId, CancellationToken ct = default);

    /// <summary>As inscrições de equipe da temporada (somente leitura).</summary>
    Task<IReadOnlyList<InscricaoDeEquipe>> ListarDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    Task<int> ContarProtocolosAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Já existe inscrição de equipe (não recusada) com este nome na temporada?</summary>
    Task<bool> ExisteNomeAsync(Guid temporadaId, string nome, CancellationToken ct = default);

    /// <summary>Quais destes CPFs já estão convidados (convite ativo) em outra inscrição de equipe não recusada da mesma categoria?</summary>
    Task<IReadOnlyList<string>> CpfsJaConvidadosAsync(Guid temporadaCategoriaId, IReadOnlyCollection<string> cpfs, CancellationToken ct = default);

    void Adicionar(InscricaoDeEquipe inscricao);

    /// <summary>Convite novo de agregado já carregado: Add explícito (ver nota sobre DbSet.Update/Modified).</summary>
    void AdicionarConvite(ConviteDeAtleta convite);
}
