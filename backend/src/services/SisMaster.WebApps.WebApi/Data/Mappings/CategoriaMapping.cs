using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class CategoriaMapping : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categorias");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).HasMaxLength(100).IsRequired();
        builder.Property(c => c.IdadeMinima).IsRequired();
        builder.Property(c => c.AceitaAbaixoIdadeMinima).IsRequired();
        builder.Property(c => c.Sexo).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.MinimoPeriodosEmQuadra).IsRequired();
        builder.Property(c => c.MinimoPeriodosForaQuadra).IsRequired();

        builder.HasOne(c => c.Associacao)
               .WithMany()
               .HasForeignKey(c => c.AssociacaoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.AssociacaoId, c.Nome }).IsUnique();

        builder.Ignore(c => c.Notificacoes);
    }
}
