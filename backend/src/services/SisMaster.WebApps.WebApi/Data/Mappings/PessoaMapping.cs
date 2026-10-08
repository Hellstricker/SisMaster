using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class PessoaMapping : IEntityTypeConfiguration<Pessoa>
{
    public void Configure(EntityTypeBuilder<Pessoa> builder)
    {
        builder.ToTable("Pessoas");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Nascimento).IsRequired();
        builder.Property(p => p.Sexo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Telefone).HasMaxLength(30);
        builder.Property(p => p.Perfil).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.OwnsOne(p => p.Cpf, cpf =>
        {
            cpf.Property(c => c.Numero)
               .HasColumnName("Cpf")
               .HasMaxLength(11)
               .IsRequired();

            cpf.HasIndex(c => c.Numero).IsUnique();
        });

        builder.Ignore(p => p.Notificacoes);
    }
}
