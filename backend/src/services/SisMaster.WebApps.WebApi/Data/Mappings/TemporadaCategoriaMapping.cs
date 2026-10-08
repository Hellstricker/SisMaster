using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class TemporadaCategoriaMapping : IEntityTypeConfiguration<TemporadaCategoria>
{
    public void Configure(EntityTypeBuilder<TemporadaCategoria> builder)
    {
        builder.ToTable("TemporadaCategorias");

        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.Nome).HasMaxLength(100).IsRequired();
        builder.Property(tc => tc.IdadeMinima).IsRequired();
        builder.Property(tc => tc.AceitaAbaixoIdadeMinima).IsRequired();
        builder.Property(tc => tc.Sexo).HasConversion<string>().HasMaxLength(20);
        builder.Property(tc => tc.MinimoPeriodosEmQuadra).IsRequired();
        builder.Property(tc => tc.MinimoPeriodosForaQuadra).IsRequired();
        builder.Property(tc => tc.Valor).HasPrecision(10, 2);

        builder.HasOne(tc => tc.Temporada)
               .WithMany(t => t.Categorias)
               .HasForeignKey(tc => tc.TemporadaId);

        builder.HasOne(tc => tc.Categoria)
               .WithMany()
               .HasForeignKey(tc => tc.CategoriaId);

        builder.HasIndex(tc => new { tc.TemporadaId, tc.CategoriaId }).IsUnique();

        builder.Ignore(tc => tc.Notificacoes);
    }
}
