using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Equipes;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class AtletaMapping : IEntityTypeConfiguration<Atleta>
{
    public void Configure(EntityTypeBuilder<Atleta> builder)
    {
        builder.ToTable("Atletas");

        builder.HasKey(a => a.Id);

        // Referência entre agregados (Equipes → Participantes): FK restrita; um pedido efetivado, um atleta.
        builder.HasOne(a => a.InscricaoCategoria)
               .WithMany()
               .HasForeignKey(a => a.InscricaoCategoriaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.InscricaoCategoriaId).IsUnique();

        builder.Ignore(a => a.Notificacoes);
    }
}
