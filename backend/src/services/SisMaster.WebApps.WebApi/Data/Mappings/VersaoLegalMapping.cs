using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Mappings;

public class VersaoLegalMapping : IEntityTypeConfiguration<VersaoLegal>
{
    public void Configure(EntityTypeBuilder<VersaoLegal> builder)
    {
        builder.ToTable("VersoesLegais");
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => new { v.AssociacaoId, v.Numero }).IsUnique();
        builder.Property(v => v.Versao).HasMaxLength(10).IsRequired();
        builder.Property(v => v.Resumo).HasMaxLength(300).IsRequired();
        builder.Property(v => v.TextoPrivacidade);   // nvarchar(max)
        builder.Property(v => v.TextoTermos);
        builder.Property(v => v.PublicadaPor).HasMaxLength(150).IsRequired();
        builder.HasOne<Associacao>().WithMany().HasForeignKey(v => v.AssociacaoId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(v => v.Notificacoes);
    }
}
