using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Time;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class JogadorMapping : IEntityTypeConfiguration<Jogador>
{
    public void Configure(EntityTypeBuilder<Jogador> builder)
    {
        builder.ToTable("Jogadores");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Numero).IsRequired();
        builder.Property(j => j.Ativo).IsRequired();

        builder.OwnsOne(j => j.Pessoa, p =>
        {
            p.Property(x => x.Nome).HasColumnName("Nome").HasMaxLength(150).IsRequired();

            p.OwnsOne(x => x.Cpf, cpf =>
            {
                cpf.Property(c => c.Numero)
                   .HasColumnName("Cpf")
                   .HasMaxLength(11)
                   .IsRequired();

                cpf.HasIndex(c => c.Numero).IsUnique();
            });
        });

        builder.Ignore(j => j.Notificacoes);
    }
}
