using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class InscricaoCategoriaMapping : IEntityTypeConfiguration<InscricaoCategoria>
{
    public void Configure(EntityTypeBuilder<InscricaoCategoria> builder)
    {
        builder.ToTable("InscricoesCategorias");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.JustificativaExcecao).HasMaxLength(500);
        builder.Property(c => c.MotivoRecusa).HasMaxLength(500);

        builder.HasOne(c => c.TemporadaCategoria)
               .WithMany()
               .HasForeignKey(c => c.TemporadaCategoriaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TemporadaCategoriaId, c.Status });

        builder.Ignore(c => c.Ativa);
        builder.Ignore(c => c.Notificacoes);
    }
}
