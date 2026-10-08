using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class PagamentoInscricaoMapping : IEntityTypeConfiguration<PagamentoInscricao>
{
    public void Configure(EntityTypeBuilder<PagamentoInscricao> builder)
    {
        builder.ToTable("PagamentosInscricao");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Valor).HasPrecision(10, 2).IsRequired();
        builder.Property(p => p.Data).IsRequired();

        builder.HasIndex(p => p.InscricaoId);

        builder.Ignore(p => p.Notificacoes);
    }
}
