using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class FaseMapping : IEntityTypeConfiguration<Fase>
{
    public void Configure(EntityTypeBuilder<Fase> builder)
    {
        builder.ToTable("Fases");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nome).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Ordem).IsRequired();
        builder.Property(f => f.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Distribuicao).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.MelhoresExtras).IsRequired();
        builder.Property(f => f.DistribuicaoManual).HasMaxLength(100);

        builder.HasOne(f => f.TemporadaCategoria)
               .WithMany()
               .HasForeignKey(f => f.TemporadaCategoriaId)
               .OnDelete(DeleteBehavior.Restrict);

        // Auto-referência: de onde vêm os classificados. Sem cascata: excluir só se nenhuma fase depende dela.
        builder.HasOne<Fase>()
               .WithMany()
               .HasForeignKey(f => f.FaseAnteriorId)
               .OnDelete(DeleteBehavior.Restrict);

        // A ordem é única na categoria, mas garantida no domínio (a troca de duas ordens no mesmo
        // SaveChanges violaria um índice único temporariamente).
        builder.HasIndex(f => new { f.TemporadaCategoriaId, f.Ordem });

        builder.Ignore(f => f.Estrutura);
        builder.Ignore(f => f.GruposManual);
        builder.Ignore(f => f.EstruturaCompleta);
        builder.Ignore(f => f.Notificacoes);
    }
}
