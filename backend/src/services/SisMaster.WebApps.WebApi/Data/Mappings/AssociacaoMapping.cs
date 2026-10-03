using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class AssociacaoMapping : IEntityTypeConfiguration<Associacao>
{
    public void Configure(EntityTypeBuilder<Associacao> builder)
    {
        builder.ToTable("Associacoes");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nome).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Uf).HasMaxLength(2).IsRequired();
        builder.Property(a => a.Ativa).IsRequired();

        builder.HasMany(a => a.Campeonatos)
               .WithOne(c => c.Associacao)
               .HasForeignKey(c => c.AssociacaoId);

        builder.Ignore(a => a.Notificacoes);
    }
}
