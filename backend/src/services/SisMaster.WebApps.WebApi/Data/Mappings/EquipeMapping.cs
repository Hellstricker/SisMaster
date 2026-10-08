using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Equipes;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class EquipeMapping : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.ToTable("Equipes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nome).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Cor).HasMaxLength(7);

        builder.HasOne(e => e.TemporadaCategoria)
               .WithMany()
               .HasForeignKey(e => e.TemporadaCategoriaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Atletas)
               .WithOne()
               .HasForeignKey(a => a.EquipeId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Atletas).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.TemporadaCategoriaId);

        builder.Ignore(e => e.Notificacoes);
    }
}
