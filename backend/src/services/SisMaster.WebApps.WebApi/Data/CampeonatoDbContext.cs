using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Partida;

namespace SisMaster.WebApps.WebApi.Data;

public class CampeonatoDbContext : DbContext, IUnitOfWork
{
    public DbSet<Associacao> Associacoes { get; set; }
    public DbSet<Campeonato> Campeonatos { get; set; }
    public DbSet<Domain.Time.Time> Times { get; set; }
    public DbSet<Domain.Time.Jogador> Jogadores { get; set; }
    public DbSet<Partida> Partidas { get; set; }
    public DbSet<EventoPartida> EventosPartida { get; set; }

    public CampeonatoDbContext(DbContextOptions<CampeonatoDbContext> options) : base(options) { }

    public async Task<bool> Commit() =>
        await SaveChangesAsync() > 0;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampeonatoDbContext).Assembly,
            t => t.Namespace?.Contains("Data.Mappings") == true);

        base.OnModelCreating(modelBuilder);
    }
}
