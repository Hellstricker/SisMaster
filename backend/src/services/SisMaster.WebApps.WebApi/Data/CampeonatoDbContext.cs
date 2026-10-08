using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data;

public class CampeonatoDbContext : DbContext, IUnitOfWork
{
    public DbSet<Associacao> Associacoes { get; set; }
    public DbSet<Campeonato> Campeonatos { get; set; }
    public DbSet<Temporada> Temporadas { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<TemporadaCategoria> TemporadaCategorias { get; set; }
    public DbSet<DescontoPorCombinacao> DescontosPorCombinacao { get; set; }
    public DbSet<Domain.Participantes.PagamentoInscricao> PagamentosInscricao { get; set; }
    public DbSet<Domain.Participantes.Pessoa> Pessoas { get; set; }
    public DbSet<Domain.Participantes.Inscricao> Inscricoes { get; set; }
    public DbSet<Domain.Participantes.InscricaoCategoria> InscricoesCategorias { get; set; }
    public DbSet<Domain.Fases.Fase> Fases { get; set; }
    public DbSet<Domain.Equipes.Equipe> Equipes { get; set; }
    public DbSet<Domain.Equipes.Atleta> Atletas { get; set; }
    public DbSet<Domain.Jogos.Local> Locais { get; set; }
    public DbSet<Domain.Jogos.Grupo> Grupos { get; set; }
    public DbSet<Domain.Jogos.FaseEquipe> FaseEquipes { get; set; }
    public DbSet<Domain.Jogos.Confronto> Confrontos { get; set; }
    public DbSet<Domain.Jogos.Jogo> Jogos { get; set; }
    public DbSet<Domain.Jogos.SorteioDeDesempate> SorteiosDeDesempate { get; set; }
    public DbSet<Domain.Sumula.Sumula> Sumulas { get; set; }
    public DbSet<Domain.Sumula.EventoSumula> EventosSumula { get; set; }
    public DbSet<Domain.Sumula.Substituicao> SubstituicoesSumula { get; set; }
    public DbSet<Domain.Sumula.SumulaDadosExternos> SumulaDadosExternos { get; set; }
    public DbSet<Domain.Sumula.Time> SumulaTimes { get; set; }
    public DbSet<Domain.Sumula.Jogador> SumulaJogadores { get; set; }

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
