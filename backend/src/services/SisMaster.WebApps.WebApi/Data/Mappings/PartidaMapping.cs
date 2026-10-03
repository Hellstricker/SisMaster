using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Partida;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class PartidaMapping : IEntityTypeConfiguration<Partida>
{
    public void Configure(EntityTypeBuilder<Partida> builder)
    {
        builder.ToTable("Partidas");

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

        builder.HasMany(p => p.Eventos)
               .WithOne(e => e.Partida)
               .HasForeignKey(e => e.PartidaId);

        builder.Ignore(p => p.Notificacoes);
    }
}
