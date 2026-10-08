using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class TemporadaMapping : IEntityTypeConfiguration<Temporada>
{
    public void Configure(EntityTypeBuilder<Temporada> builder)
    {
        builder.ToTable("Temporadas");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Ano).IsRequired();
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.DataInicioInscricoes).IsRequired();
        builder.Property(t => t.DataFimInscricoes).IsRequired();
        builder.Property(t => t.InscricoesHabilitadas).IsRequired();
        builder.Ignore(t => t.CadastroFasesEncerrado);
        builder.Ignore(t => t.TabelaJogosGerada);
        builder.Property(t => t.ValorBonificacaoPorAtleta).HasPrecision(6, 2).IsRequired();
        builder.Property(t => t.TaxaInscricao).HasPrecision(10, 2);

        builder.HasMany(t => t.Descontos)
               .WithOne()
               .HasForeignKey(d => d.TemporadaId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Descontos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(t => t.Campeonato)
               .WithMany(c => c.Temporadas)
               .HasForeignKey(t => t.CampeonatoId);

        builder.Ignore(t => t.Notificacoes);
    }
}
