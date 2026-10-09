using SisMaster.WebApi.Core.Usuarios;
using SisMaster.WebApps.WebApi.Domain.Auditoria;

namespace SisMaster.WebApps.WebApi.Data.Auditoria;

public sealed class ServicoDeAuditoria(CampeonatoDbContext contexto, IAcessoDoUsuario acesso) : IAuditoria
{
    public void Registrar(string acao, string entidade, Guid? entidadeId, string detalhe) =>
        contexto.RegistrosDeAuditoria.Add(new RegistroDeAuditoria(acesso.Id, acao, entidade, entidadeId, detalhe));

    public async Task RegistrarEGravarAsync(string acao, string entidade, Guid? entidadeId, string detalhe, CancellationToken ct = default)
    {
        Registrar(acao, entidade, entidadeId, detalhe);
        await contexto.SaveChangesAsync(ct);
    }
}
