using SisMaster.Core.Data;
using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

/// <summary>
/// O conteúdo das páginas "A associação" e "Contato" do site público, uma por associação: missão, quem somos, diretoria e canais
/// de contato. Texto simples (nunca HTML). Só aparece no site o que está preenchido.
/// </summary>
public class PaginaPublica : Entity, IAggregateRoot
{
    public const int MaxMembros = 30;

    public Guid AssociacaoId { get; private set; }
    public string? Missao { get; private set; }
    public string? QuemSomos { get; private set; }
    public string? GestaoDaDiretoria { get; private set; }
    public string? Whatsapp { get; private set; }
    public string? Instagram { get; private set; }
    public string? Email { get; private set; }
    public string? OndeJogamos { get; private set; }

    private readonly List<MembroDaDiretoria> _diretoria = [];
    public IReadOnlyCollection<MembroDaDiretoria> Diretoria => _diretoria.AsReadOnly();

    protected PaginaPublica() { }

    public PaginaPublica(Guid associacaoId) => AssociacaoId = associacaoId;

    /// <summary>
    /// Substitui o conteúdo. A diretoria é substituída inteira (a ordem é a da lista); devolve o que saiu e o que entrou para o repositório gravar.
    /// </summary>
    public (IReadOnlyList<MembroDaDiretoria> Removidos, IReadOnlyList<MembroDaDiretoria> Adicionados) Atualizar(
        string? missao, string? quemSomos, string? gestao, string? whatsapp, string? instagram, string? email, string? ondeJogamos,
        IReadOnlyList<(string Cargo, string? Nome)> diretoria)
    {
        Missao = Limpar(missao, 160, "A missão deve ter até 160 caracteres");
        QuemSomos = Limpar(quemSomos, 2000, "O texto \"Quem somos\" deve ter até 2.000 caracteres");
        GestaoDaDiretoria = Limpar(gestao, 30, "A gestão deve ter até 30 caracteres");
        Whatsapp = Limpar(whatsapp, 30, "O WhatsApp deve ter até 30 caracteres");
        Instagram = Limpar(instagram, 60, "O Instagram deve ter até 60 caracteres");
        Email = Limpar(email, 150, "O e-mail deve ter até 150 caracteres");
        if (Email is not null)
            Validacoes.ValidarSeFalso(Email.Contains('@') && Email.IndexOf('@') > 0 && Email.IndexOf('@') < Email.Length - 1 && !Email.Contains(' '), "E-mail inválido");
        OndeJogamos = Limpar(ondeJogamos, 150, "O local deve ter até 150 caracteres");

        Validacoes.ValidarSeVerdadeiro(diretoria.Count > MaxMembros, $"A diretoria pode ter até {MaxMembros} cargos");
        var novos = new List<MembroDaDiretoria>();
        foreach (var (cargo, nome) in diretoria)
            novos.Add(new MembroDaDiretoria(Id, cargo, nome, novos.Count + 1));

        var removidos = _diretoria.ToList();
        _diretoria.Clear();
        _diretoria.AddRange(novos);
        return (removidos, novos);
    }

    private static string? Limpar(string? valor, int maximo, string mensagem)
    {
        var v = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        if (v is not null) Validacoes.ValidarSeVerdadeiro(v.Length > maximo, mensagem);
        return v;
    }
}

/// <summary>Um cargo da diretoria. Cargo sem nome fica guardado, mas não aparece no site.</summary>
public class MembroDaDiretoria : Entity
{
    public Guid PaginaPublicaId { get; private set; }
    public string Cargo { get; private set; } = string.Empty;
    public string? Nome { get; private set; }
    public int Ordem { get; private set; }

    protected MembroDaDiretoria() { }

    public MembroDaDiretoria(Guid paginaId, string cargo, string? nome, int ordem)
    {
        Validacoes.ValidarSeVazio(cargo, "Informe o cargo");
        Validacoes.ValidarSeVerdadeiro(cargo.Trim().Length > 60, "O cargo deve ter até 60 caracteres");
        var n = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
        if (n is not null) Validacoes.ValidarSeVerdadeiro(n.Length > 100, "O nome deve ter até 100 caracteres");
        PaginaPublicaId = paginaId;
        Cargo = cargo.Trim();
        Nome = n;
        Ordem = ordem;
    }
}

public interface IPaginaPublicaRepository : IRepository<PaginaPublica>
{
    /// <summary>A página da associação (rastreada, com a diretoria) ou nula se nunca foi preenchida.</summary>
    Task<PaginaPublica?> ObterDaAssociacaoAsync(Guid associacaoId, CancellationToken ct = default);
    void Adicionar(PaginaPublica pagina);
    void AdicionarMembros(IEnumerable<MembroDaDiretoria> membros);
    void RemoverMembros(IEnumerable<MembroDaDiretoria> membros);
}
