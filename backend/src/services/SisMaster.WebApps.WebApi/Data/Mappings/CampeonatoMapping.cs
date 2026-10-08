using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class CampeonatoMapping : IEntityTypeConfiguration<Campeonato>
{
    public void Configure(EntityTypeBuilder<Campeonato> builder)
    {
        builder.ToTable("Campeonatos");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Ativo).IsRequired();

        builder.HasMany(c => c.Temporadas)
               .WithOne(t => t.Campeonato)
               .HasForeignKey(t => t.CampeonatoId);

        builder.Ignore(c => c.Notificacoes);
    }
}
