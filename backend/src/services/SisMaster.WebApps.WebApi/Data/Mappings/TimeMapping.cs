using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class TimeMapping : IEntityTypeConfiguration<Domain.Time.Time>
{
    public void Configure(EntityTypeBuilder<Domain.Time.Time> builder)
    {
        builder.ToTable("Times");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nome).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Sigla).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Ativo).IsRequired();

        builder.HasMany(t => t.Jogadores)
               .WithOne(j => j.Time)
               .HasForeignKey(j => j.TimeId);

        builder.Ignore(t => t.Notificacoes);
    }
}
