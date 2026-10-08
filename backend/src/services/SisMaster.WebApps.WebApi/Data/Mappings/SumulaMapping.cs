using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class SumulaMapping : IEntityTypeConfiguration<Sumula>
{
    public void Configure(EntityTypeBuilder<Sumula> builder)
    {
        builder.ToTable("Sumulas");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.PeriodoAtual).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.PlacarCasa).IsRequired();
        builder.Property(p => p.PlacarVisitante).IsRequired();
        builder.Property(p => p.FaltasCasa).IsRequired();
        builder.Property(p => p.FaltasVisitante).IsRequired();
        builder.Property(p => p.CronometroAtivo).IsRequired();
        builder.Property(p => p.CronometroSegundosRestantes).IsRequired();
        builder.Property(p => p.CronometroIniciadoEm);
        builder.Property(p => p.CronometroSegundosAoIniciar).IsRequired();
        builder.Property(p => p.ShotClockAtivo).IsRequired();
        builder.Property(p => p.ShotClockSegundosRestantes).IsRequired();
        builder.Property(p => p.ShotClockIniciadoEm);
        builder.Property(p => p.ShotClockSegundosAoIniciar).IsRequired();
        builder.Property(p => p.PosseCasa).IsRequired();
        builder.Property(p => p.CodigoExterno).HasMaxLength(50);
        builder.Property(p => p.ImportadaEm);
        builder.HasIndex(p => p.CodigoExterno).IsUnique().HasFilter("[CodigoExterno] IS NOT NULL");

        // Referência ao Jogo (contexto Campeonato) só por Id, sem FK: uma súmula por jogo.
        builder.HasIndex(p => p.JogoId).IsUnique();

        builder.HasMany(p => p.Eventos)
               .WithOne(e => e.Sumula)
               .HasForeignKey(e => e.SumulaId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Eventos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Substituicoes)
               .WithOne()
               .HasForeignKey(x => x.SumulaId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Substituicoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Times)
               .WithOne()
               .HasForeignKey(t => t.SumulaId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Times).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.Notificacoes);
    }
}

/// <summary>Table splitting: o JSON bruto da importação mora na linha da súmula, mas só é lido quando pedido.</summary>
public class SumulaDadosExternosMapping : IEntityTypeConfiguration<SumulaDadosExternos>
{
    public void Configure(EntityTypeBuilder<SumulaDadosExternos> builder)
    {
        builder.ToTable("Sumulas");
        builder.HasKey(d => d.SumulaId);
        builder.Property(d => d.SumulaId).ValueGeneratedNever().HasColumnName("Id");
        builder.Property(d => d.Json).HasColumnName("DadosExternosJson").HasColumnType("nvarchar(max)").IsRequired();

        builder.HasOne<Sumula>().WithOne().HasForeignKey<SumulaDadosExternos>(d => d.SumulaId);
    }
}

public class EventoSumulaMapping: IEntityTypeConfiguration<EventoSumula>
{
    public void Configure(EntityTypeBuilder<EventoSumula> builder)
    {
        builder.ToTable("EventosSumula");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.JogadorId).IsRequired();
        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Periodo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.TempoJogoSegundos).IsRequired();
        builder.Property(e => e.RegistradoEm).IsRequired();

        builder.Ignore(e => e.Notificacoes);
    }
}

public class SubstituicaoMapping : IEntityTypeConfiguration<Substituicao>
{
    public void Configure(EntityTypeBuilder<Substituicao> builder)
    {
        builder.ToTable("SubstituicoesSumula");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Ordem).IsRequired();
        builder.Property(x => x.JogadorEntraId).IsRequired();
        builder.Property(x => x.Periodo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.TempoJogoSegundos).IsRequired();
        builder.Property(x => x.NoInicio).IsRequired();
        builder.Property(x => x.RegistradoEm).IsRequired();
        builder.HasIndex(x => new { x.SumulaId, x.Ordem }).IsUnique();
        builder.Ignore(x => x.Notificacoes);
    }
}

public class TimeMapping : IEntityTypeConfiguration<Time>
{
    public void Configure(EntityTypeBuilder<Time> builder)
    {
        builder.ToTable("SumulaTimes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Lado).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Cor).HasMaxLength(7);
        builder.Property(t => t.Tecnico).HasMaxLength(100);
        builder.Property(t => t.AuxiliarTecnico).HasMaxLength(100);

        // Equipe e capitão só por Id (a Equipe é de outro contexto; o capitão é um Jogador do próprio time).
        builder.HasIndex(t => new { t.SumulaId, t.Lado }).IsUnique();

        builder.HasMany(t => t.Jogadores)
               .WithOne()
               .HasForeignKey(j => j.TimeId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Jogadores).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(t => t.Notificacoes);
    }
}

public class JogadorMapping : IEntityTypeConfiguration<Jogador>
{
    public void Configure(EntityTypeBuilder<Jogador> builder)
    {
        builder.ToTable("SumulaJogadores");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Nome).HasMaxLength(150).IsRequired();
        builder.Property(j => j.Numero).HasMaxLength(2).IsRequired();
        builder.Property(j => j.ChegouNoInicio).HasDefaultValue(true);

        builder.HasIndex(j => new { j.TimeId, j.AtletaId }).IsUnique();
        builder.HasIndex(j => new { j.TimeId, j.Numero }).IsUnique();

        builder.Ignore(j => j.Notificacoes);
    }
}
