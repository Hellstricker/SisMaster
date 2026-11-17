using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Torneio;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class InscricaoDeEquipeMapping : IEntityTypeConfiguration<InscricaoDeEquipe>
{
    public void Configure(EntityTypeBuilder<InscricaoDeEquipe> builder)
    {
        builder.ToTable("InscricoesDeEquipe");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ContaId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.ResponsavelNome).HasMaxLength(150).IsRequired();
        builder.Property(i => i.ResponsavelEmail).HasMaxLength(150).IsRequired();
        builder.Property(i => i.ResponsavelTelefone).HasMaxLength(30);
        builder.Property(i => i.Nome).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Protocolo).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.MotivoRecusa).HasMaxLength(500);
        builder.Property(i => i.JustificativaExcecao).HasMaxLength(500);

        builder.HasIndex(i => new { i.TemporadaId, i.Protocolo }).IsUnique();
        builder.HasIndex(i => i.CodigoDeEnvio).IsUnique();
        builder.HasIndex(i => i.ContaId);

        builder.HasOne<Temporada>().WithMany().HasForeignKey(i => i.TemporadaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TemporadaCategoria>().WithMany().HasForeignKey(i => i.TemporadaCategoriaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Equipe>().WithMany().HasForeignKey(i => i.EquipeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Convites).WithOne().HasForeignKey(c => c.InscricaoDeEquipeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Convites).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(i => i.Ativos);
        builder.Ignore(i => i.Confirmados);
        builder.Ignore(i => i.ConvitesEmAberto);
        builder.Ignore(i => i.AguardandoConfirmacoes);
        builder.Ignore(i => i.Notificacoes);
    }
}

public class ConviteDeAtletaMapping : IEntityTypeConfiguration<ConviteDeAtleta>
{
    public void Configure(EntityTypeBuilder<ConviteDeAtleta> builder)
    {
        builder.ToTable("ConvitesDeAtleta");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Cpf).HasMaxLength(11);
        builder.Property(c => c.Sexo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(150).IsRequired();
        builder.Property(c => c.CodigoHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(c => c.CodigoHash);
        builder.HasIndex(c => c.InscricaoId);

        builder.Ignore(c => c.Notificacoes);
    }
}
