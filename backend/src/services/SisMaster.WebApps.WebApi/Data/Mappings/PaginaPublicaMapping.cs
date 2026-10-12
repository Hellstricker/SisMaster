using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class PaginaPublicaMapping : IEntityTypeConfiguration<PaginaPublica>
{
    public void Configure(EntityTypeBuilder<PaginaPublica> builder)
    {
        builder.ToTable("PaginasPublicas");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.AssociacaoId).IsUnique();   // uma página por associação
        builder.Property(p => p.Missao).HasMaxLength(160);
        builder.Property(p => p.QuemSomos).HasMaxLength(2000);
        builder.Property(p => p.GestaoDaDiretoria).HasMaxLength(30);
        builder.Property(p => p.Whatsapp).HasMaxLength(30);
        builder.Property(p => p.Instagram).HasMaxLength(60);
        builder.Property(p => p.Email).HasMaxLength(150);
        builder.Property(p => p.OndeJogamos).HasMaxLength(150);

        builder.HasOne<Associacao>().WithMany().HasForeignKey(p => p.AssociacaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Diretoria).WithOne().HasForeignKey(m => m.PaginaPublicaId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Diretoria).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(p => p.Notificacoes);
    }
}

public class MembroDaDiretoriaMapping : IEntityTypeConfiguration<MembroDaDiretoria>
{
    public void Configure(EntityTypeBuilder<MembroDaDiretoria> builder)
    {
        builder.ToTable("MembrosDaDiretoria");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Cargo).HasMaxLength(60).IsRequired();
        builder.Property(m => m.Nome).HasMaxLength(100);
        builder.Ignore(m => m.Notificacoes);
    }
}
