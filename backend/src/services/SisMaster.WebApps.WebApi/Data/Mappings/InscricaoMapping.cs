using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class InscricaoMapping : IEntityTypeConfiguration<Inscricao>
{
    public void Configure(EntityTypeBuilder<Inscricao> builder)
    {
        builder.ToTable("Inscricoes");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Posicao).HasMaxLength(50);
        builder.Property(i => i.NomePlanoSaude).HasMaxLength(100);
        builder.Property(i => i.DataEnvio).IsRequired();
        builder.Property(i => i.ConsentimentoLgpdEm).IsRequired();

        builder.HasOne(i => i.Pessoa)
               .WithMany()
               .HasForeignKey(i => i.PessoaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Temporada>()
               .WithMany()
               .HasForeignKey(i => i.TemporadaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Categorias)
               .WithOne(c => c.Inscricao)
               .HasForeignKey(c => c.InscricaoId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Categorias).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(i => i.ValorTotal).HasPrecision(10, 2);
        builder.Property(i => i.ValorDesconto).HasPrecision(10, 2);

        builder.HasMany(i => i.Pagamentos)
               .WithOne()
               .HasForeignKey(p => p.InscricaoId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Pagamentos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(i => i.TaxaPaga);
        builder.Ignore(i => i.TotalPago);
        builder.Ignore(i => i.CobrancaGerada);
        builder.Ignore(i => i.ValorFinal);
        builder.Ignore(i => i.Saldo);
        builder.Ignore(i => i.CategoriasCobradas);

        builder.HasIndex(i => new { i.TemporadaId, i.PessoaId });

        builder.Ignore(i => i.Notificacoes);
    }
}
