using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SisMaster.WebApi.Core.Autorizacao;

namespace SisMaster.WebApi.Core.Usuarios;

/// <summary>O usuário da requisição atual: perfil e associações a que tem acesso (lidos do token).</summary>
public interface IAcessoDoUsuario
{
    bool Autenticado { get; }
    string? Id { get; }
    bool EhAdministrador { get; }
    bool EhAdministradorDeAssociacao { get; }
    bool EhArbitro { get; }

    /// <summary>Associações vinculadas. Vazio para o Administrador (que acessa todas).</summary>
    IReadOnlySet<Guid> Associacoes { get; }

    /// <summary>O Administrador acessa qualquer associação; os demais, só as vinculadas.</summary>
    bool PodeAcessar(Guid associacaoId);

    /// <summary>Nulo = sem restrição (Administrador); caso contrário, as associações permitidas (para filtrar listas).</summary>
    IReadOnlyCollection<Guid>? AssociacoesPermitidas { get; }
}

public sealed class AcessoDoUsuario : IAcessoDoUsuario
{
    private readonly ClaimsPrincipal? _usuario;
    private readonly Lazy<HashSet<Guid>> _associacoes;

    public AcessoDoUsuario(IHttpContextAccessor accessor) : this(accessor.HttpContext?.User) { }

    public AcessoDoUsuario(ClaimsPrincipal? usuario)
    {
        _usuario = usuario;
        _associacoes = new Lazy<HashSet<Guid>>(() =>
            _usuario?.FindAll(TiposDeClaim.Associacao)
                .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty).ToHashSet() ?? []);
    }

    public bool Autenticado => _usuario?.Identity?.IsAuthenticated == true;
    public string? Id => _usuario?.FindFirst("sub")?.Value ?? _usuario?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public bool EhAdministrador => _usuario?.IsInRole(Perfis.Administrador) == true;
    public bool EhAdministradorDeAssociacao => _usuario?.IsInRole(Perfis.AdministradorDeAssociacao) == true;
    public bool EhArbitro => _usuario?.IsInRole(Perfis.Arbitro) == true;
    public IReadOnlySet<Guid> Associacoes => EhAdministrador ? new HashSet<Guid>() : _associacoes.Value;
    public bool PodeAcessar(Guid associacaoId) => Autenticado && (EhAdministrador || _associacoes.Value.Contains(associacaoId));
    public IReadOnlyCollection<Guid>? AssociacoesPermitidas => EhAdministrador ? null : _associacoes.Value;
}
