using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

internal static class ReferenciaEquipeMapping
{
    /// <summary>Colunas da referência com o prefixo do dono. Fase/confronto são só Guid, sem FK (evita ciclos).</summary>
    public static void Configurar(OwnedNavigationBuilder<FaseEquipe, ReferenciaEquipe> b) => ConfigurarComum(b);

    public static void Configurar(OwnedNavigationBuilder<Confronto, ReferenciaEquipe> b) => ConfigurarComum(b);

    private static void ConfigurarComum<T>(OwnedNavigationBuilder<T, ReferenciaEquipe> b) where T : class
    {
        b.Property(r => r.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(r => r.EquipeId);
        b.Property(r => r.FaseId);
        b.Property(r => r.GrupoOrdem);
        b.Property(r => r.Posicao);
        b.Property(r => r.ConfrontoOrigemId);
    }
}

public class LocalMapping : IEntityTypeConfiguration<Local>
{
    public void Configure(EntityTypeBuilder<Local> builder)
    {
        builder.ToTable("Locais");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Nome).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Cidade).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Estado).HasMaxLength(2);

        builder.HasOne<Associacao>().WithMany().HasForeignKey(l => l.AssociacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.AssociacaoId, l.Nome, l.Cidade }).IsUnique();

        builder.Ignore(l => l.Notificacoes);
    }
}

public class GrupoMapping : IEntityTypeConfiguration<Grupo>
{
    public void Configure(EntityTypeBuilder<Grupo> builder)
    {
        builder.ToTable("Grupos");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Nome).HasMaxLength(10).IsRequired();

        builder.HasOne<Fase>().WithMany().HasForeignKey(g => g.FaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(g => new { g.FaseId, g.Ordem }).IsUnique();

        builder.Ignore(g => g.Notificacoes);
    }
}

public class FaseEquipeMapping : IEntityTypeConfiguration<FaseEquipe>
{
    public void Configure(EntityTypeBuilder<FaseEquipe> builder)
    {
        builder.ToTable("FaseEquipes");
        builder.HasKey(v => v.Id);

        builder.HasOne<Fase>().WithMany().HasForeignKey(v => v.FaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(v => v.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Equipe).WithMany().HasForeignKey(v => v.EquipeId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(v => v.Origem, ReferenciaEquipeMapping.Configurar);
        builder.Navigation(v => v.Origem).IsRequired();

        builder.HasIndex(v => new { v.FaseId, v.Posicao }).IsUnique();

        builder.Ignore(v => v.Notificacoes);
    }
}

public class ConfrontoMapping : IEntityTypeConfiguration<Confronto>
{
    public void Configure(EntityTypeBuilder<Confronto> builder)
    {
        builder.ToTable("Confrontos");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nome).HasMaxLength(60).IsRequired();

        builder.HasOne<Fase>().WithMany().HasForeignKey(c => c.FaseId).OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(c => c.OrigemA, ReferenciaEquipeMapping.Configurar);
        builder.OwnsOne(c => c.OrigemB, ReferenciaEquipeMapping.Configurar);
        builder.Navigation(c => c.OrigemA).IsRequired();
        builder.Navigation(c => c.OrigemB).IsRequired();

        builder.HasIndex(c => new { c.FaseId, c.Numero }).IsUnique();

        builder.Ignore(c => c.Notificacoes);
    }
}

public class JogoMapping : IEntityTypeConfiguration<Jogo>
{
    public void Configure(EntityTypeBuilder<Jogo> builder)
    {
        builder.ToTable("Jogos");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Temporada>().WithMany().HasForeignKey(j => j.TemporadaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Fase>().WithMany().HasForeignKey(j => j.FaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(j => j.GrupoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Confronto>().WithMany().HasForeignKey(j => j.ConfrontoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(j => j.Casa).WithMany().HasForeignKey(j => j.CasaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(j => j.Visitante).WithMany().HasForeignKey(j => j.VisitanteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(j => j.Local).WithMany().HasForeignKey(j => j.LocalId).OnDelete(DeleteBehavior.Restrict);

        // O número do jogo é único na temporada (compartilhado entre categorias).
        builder.HasIndex(j => new { j.TemporadaId, j.Numero }).IsUnique();
        builder.HasIndex(j => j.FaseId);

        builder.Ignore(j => j.Notificacoes);
    }
}

public class SorteioDeDesempateMapping : IEntityTypeConfiguration<SorteioDeDesempate>
{
    public void Configure(EntityTypeBuilder<SorteioDeDesempate> builder)
    {
        builder.ToTable("SorteiosDeDesempate");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Ordem).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DefinidoEm).IsRequired();

        // Uma tabela (fase ou grupo) tem no máximo um sorteio; referências só por Id.
        builder.HasIndex(x => new { x.FaseId, x.GrupoId }).IsUnique();

        builder.Ignore(x => x.OrdemDasVagas);
        builder.Ignore(x => x.Notificacoes);
    }
}
