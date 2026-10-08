using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class DescontoPorCombinacaoMapping : IEntityTypeConfiguration<DescontoPorCombinacao>
{
    public void Configure(EntityTypeBuilder<DescontoPorCombinacao> builder)
    {
        builder.ToTable("DescontosPorCombinacao");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Combinacao).HasMaxLength(800).IsRequired();
        builder.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.Valor).HasPrecision(10, 2).IsRequired();

        // Uma combinação de categorias tem no máximo um desconto por temporada.
        builder.HasIndex(d => new { d.TemporadaId, d.Combinacao }).IsUnique();

        builder.Ignore(d => d.TemporadaCategoriaIds);
        builder.Ignore(d => d.Notificacoes);
    }
}
