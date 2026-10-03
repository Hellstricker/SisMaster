using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Partida;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class EventoPartidaMapping : IEntityTypeConfiguration<EventoPartida>
{
    public void Configure(EntityTypeBuilder<EventoPartida> builder)
    {
        builder.ToTable("EventosPartida");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.JogadorId).IsRequired();
        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Periodo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.TempoJogoSegundos).IsRequired();
        builder.Property(e => e.RegistradoEm).IsRequired();

        builder.Ignore(e => e.Notificacoes);
    }
}
