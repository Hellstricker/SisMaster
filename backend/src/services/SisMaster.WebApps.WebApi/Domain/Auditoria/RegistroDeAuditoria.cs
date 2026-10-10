using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Auditoria;

/// <summary>
/// Quem fez o quê, e quando, nas ações sensíveis (estorno de pagamento, correção de identificação, exportação de dados,
/// importação de planilha, desfazer aprovação...). Só se acrescenta; nunca se altera nem se apaga.
/// </summary>
public class RegistroDeAuditoria : Entity
{
    public DateTime Em { get; private set; }

    /// <summary>Id do usuário que fez a ação (da Identidade).</summary>
    public string? UsuarioId { get; private set; }

    public string Acao { get; private set; } = string.Empty;
    public string Entidade { get; private set; } = string.Empty;
    public Guid? EntidadeId { get; private set; }
    public string Detalhe { get; private set; } = string.Empty;

    protected RegistroDeAuditoria() { }

    public RegistroDeAuditoria(string? usuarioId, string acao, string entidade, Guid? entidadeId, string detalhe)
    {
        Em = DateTime.UtcNow;
        UsuarioId = usuarioId;
        Acao = acao;
        Entidade = entidade;
        EntidadeId = entidadeId;
        Detalhe = detalhe.Length > 1000 ? detalhe[..1000] : detalhe;
    }
}

public interface IAuditoria
{
    /// <summary>Acrescenta o registro ao mesmo contexto da operação: é gravado junto no próximo Commit.</summary>
    void Registrar(string acao, string entidade, Guid? entidadeId, string detalhe);

    /// <summary>Para ações sem outra gravação (por exemplo, exportar): grava na hora.</summary>
    Task RegistrarEGravarAsync(string acao, string entidade, Guid? entidadeId, string detalhe, CancellationToken ct = default);

    /// <summary>O histórico de uma entidade (somente leitura), do mais antigo ao mais recente.</summary>
    Task<IReadOnlyList<RegistroDeAuditoria>> ListarAsync(string entidade, Guid entidadeId, CancellationToken ct = default);
}
