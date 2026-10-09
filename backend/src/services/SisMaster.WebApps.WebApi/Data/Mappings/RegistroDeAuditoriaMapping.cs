using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Auditoria;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class RegistroDeAuditoriaMapping : IEntityTypeConfiguration<RegistroDeAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroDeAuditoria> builder)
    {
        builder.ToTable("RegistrosDeAuditoria");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Em).IsRequired();
        builder.Property(r => r.UsuarioId).HasMaxLength(450);
        builder.Property(r => r.Acao).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Entidade).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Detalhe).HasMaxLength(1000).IsRequired();
        builder.HasIndex(r => new { r.Entidade, r.EntidadeId });
        builder.HasIndex(r => r.Em);
        builder.Ignore(r => r.Notificacoes);
    }
}
