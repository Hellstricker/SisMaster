using MediatR;
using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Communications;
using SisMaster.Core.Messages.CommonMessages.Notifications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Commands.Handlers;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Data.Repositories;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Application.Importacao.Fiba;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Configurations;

public static class DependenciesConfiguration
{
    public static IServiceCollection AddDependenciesConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // MediatR + Notifications
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependenciesConfiguration).Assembly));
        services.AddScoped<IMediatorHandler, MediatorHandler>();
        services.AddScoped<INotificationHandler<DomainNotification>, DomainNotificationHandler>();

        // DbContext
        var connectionString = configuration.GetConnectionString("CampeonatoConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string do banco não configurada. Defina a variável ConnectionStrings__CampeonatoConnection " +
                "(no Docker ela vem do .env; fora dele, use scripts/api-dev.ps1).");

        services.AddDbContext<CampeonatoDbContext>(options => options.UseSqlServer(connectionString));

        // Repositories
        services.AddScoped<IAssociacaoRepository, AssociacaoRepository>();
        services.AddScoped<ICampeonatoRepository, CampeonatoRepository>();
        services.AddScoped<ITemporadaRepository, TemporadaRepository>();
        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IPessoaRepository, PessoaRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<IEquipeRepository, EquipeRepository>();
        services.AddScoped<IFaseRepository, FaseRepository>();
        services.AddScoped<IJogoRepository, JogoRepository>();
        services.AddScoped<ILocalRepository, LocalRepository>();
        services.AddScoped<DetalheFaseQuery>();
        services.AddScoped<ClassificacaoFaseQuery>();
        services.AddScoped<AvancoDaTemporada>();
        services.AddScoped<BonificacaoDosJogos>();
        services.AddScoped<RodizioDaSumulaQuery>();
        services.AddScoped<JogosDoArbitroQuery>();
        services.AddScoped<JogosDaTemporadaQuery>();
        services.AddScoped<DetalheJogoQuery>();
        services.AddScoped<PreparoSumulaQuery>();
        services.AddScoped<ImportacaoFibaPreviaQuery>();
        services.AddHttpClient<IFibaLiveStatsClient, FibaLiveStatsClient>(c =>
            c.BaseAddress = new Uri("https://fibalivestats.dcd.shared.geniussports.com/"));
        services.AddScoped<ISumulaRepository, SumulaRepository>();

        // Command Handlers
        services.AddScoped<IRequestHandler<CriarAssociacaoCommand, FluentValidation.Results.ValidationResult>, CriarAssociacaoCommandHandler>();
        services.AddScoped<IRequestHandler<AlterarStatusAssociacaoCommand, FluentValidation.Results.ValidationResult>, AlterarStatusAssociacaoCommandHandler>();
        services.AddScoped<IRequestHandler<EditarAssociacaoCommand, FluentValidation.Results.ValidationResult>, EditarAssociacaoCommandHandler>();
        services.AddScoped<IRequestHandler<CriarCampeonatoCommand, FluentValidation.Results.ValidationResult>, CriarCampeonatoCommandHandler>();
        services.AddScoped<IRequestHandler<CriarCategoriaCommand, FluentValidation.Results.ValidationResult>, CriarCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<ExcluirCategoriaCommand, FluentValidation.Results.ValidationResult>, ExcluirCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<EditarCategoriaCommand, FluentValidation.Results.ValidationResult>, EditarCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<ConfigurarElegibilidadeCategoriaCommand, FluentValidation.Results.ValidationResult>, ConfigurarElegibilidadeCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<DefinirValorCategoriaCommand, FluentValidation.Results.ValidationResult>, DefinirValorCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<AlterarInscricoesHabilitadasCommand, FluentValidation.Results.ValidationResult>, AlterarInscricoesHabilitadasCommandHandler>();
        services.AddScoped<IRequestHandler<ConfigurarBonificacaoCommand, FluentValidation.Results.ValidationResult>, ConfigurarBonificacaoCommandHandler>();
        services.AddScoped<IRequestHandler<CriarTemporadaCommand, FluentValidation.Results.ValidationResult>, CriarTemporadaCommandHandler>();
        services.AddScoped<IRequestHandler<EncerrarInscricoesCommand, FluentValidation.Results.ValidationResult>, EncerrarInscricoesCommandHandler>();
        services.AddScoped<IRequestHandler<IniciarTemporadaCommand, FluentValidation.Results.ValidationResult>, IniciarTemporadaCommandHandler>();
        services.AddScoped<IRequestHandler<EncerrarTemporadaCommand, FluentValidation.Results.ValidationResult>, EncerrarTemporadaCommandHandler>();
        services.AddScoped<IRequestHandler<AdicionarCategoriaCommand, FluentValidation.Results.ValidationResult>, AdicionarCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<EditarLocalCommand, FluentValidation.Results.ValidationResult>, EditarLocalCommandHandler>();
        services.AddScoped<IRequestHandler<ExcluirLocalCommand, FluentValidation.Results.ValidationResult>, ExcluirLocalCommandHandler>();
        services.AddScoped<IRequestHandler<DefinirDistribuicaoManualCommand, FluentValidation.Results.ValidationResult>, DefinirDistribuicaoManualCommandHandler>();
        services.AddScoped<IRequestHandler<DefinirSorteioCommand, FluentValidation.Results.ValidationResult>, DefinirSorteioCommandHandler>();
        services.AddScoped<IRequestHandler<EditarCampeonatoCommand, FluentValidation.Results.ValidationResult>, EditarCampeonatoCommandHandler>();
        services.AddScoped<IRequestHandler<ImportarFibaCommand, FluentValidation.Results.ValidationResult>, ImportarFibaCommandHandler>();
        services.AddScoped<IRequestHandler<AcrescentarAtletaNaSumulaCommand, FluentValidation.Results.ValidationResult>, AcrescentarAtletaNaSumulaCommandHandler>();
        services.AddScoped<IRequestHandler<RemoverCategoriaDaTemporadaCommand, FluentValidation.Results.ValidationResult>, RemoverCategoriaDaTemporadaCommandHandler>();
        services.AddScoped<IRequestHandler<EnviarInscricaoCommand, FluentValidation.Results.ValidationResult>, EnviarInscricaoCommandHandler>();
        services.AddScoped<IRequestHandler<AprovarInscricaoCategoriaCommand, FluentValidation.Results.ValidationResult>, AprovarInscricaoCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<RegistrarPagamentoTaxaCommand, FluentValidation.Results.ValidationResult>, RegistrarPagamentoTaxaCommandHandler>();
        services.AddScoped<IRequestHandler<RegistrarPagamentoSaldoCommand, FluentValidation.Results.ValidationResult>, RegistrarPagamentoSaldoCommandHandler>();
        services.AddScoped<IRequestHandler<DefinirTaxaInscricaoCommand, FluentValidation.Results.ValidationResult>, DefinirTaxaInscricaoCommandHandler>();
        services.AddScoped<IRequestHandler<DefinirDescontoCommand, FluentValidation.Results.ValidationResult>, DefinirDescontoCommandHandler>();
        services.AddScoped<IRequestHandler<RemoverDescontoCommand, FluentValidation.Results.ValidationResult>, RemoverDescontoCommandHandler>();
        services.AddScoped<IRequestHandler<GerarCobrancasCommand, FluentValidation.Results.ValidationResult>, GerarCobrancasCommandHandler>();
        services.AddScoped<IRequestHandler<RecusarInscricaoCategoriaCommand, FluentValidation.Results.ValidationResult>, RecusarInscricaoCategoriaCommandHandler>();
        services.AddScoped<IRequestHandler<CriarEquipeCommand, FluentValidation.Results.ValidationResult>, CriarEquipeCommandHandler>();
        services.AddScoped<IRequestHandler<EditarEquipeCommand, FluentValidation.Results.ValidationResult>, EditarEquipeCommandHandler>();
        services.AddScoped<IRequestHandler<ExcluirEquipeCommand, FluentValidation.Results.ValidationResult>, ExcluirEquipeCommandHandler>();
        services.AddScoped<IRequestHandler<AdicionarAtletaCommand, FluentValidation.Results.ValidationResult>, AdicionarAtletaCommandHandler>();
        services.AddScoped<IRequestHandler<RemoverAtletaCommand, FluentValidation.Results.ValidationResult>, RemoverAtletaCommandHandler>();
        services.AddScoped<IRequestHandler<CriarFaseCommand, FluentValidation.Results.ValidationResult>, CriarFaseCommandHandler>();
        services.AddScoped<IRequestHandler<EditarFaseCommand, FluentValidation.Results.ValidationResult>, EditarFaseCommandHandler>();
        services.AddScoped<IRequestHandler<ExcluirFaseCommand, FluentValidation.Results.ValidationResult>, ExcluirFaseCommandHandler>();
        services.AddScoped<IRequestHandler<MoverFaseCommand, FluentValidation.Results.ValidationResult>, MoverFaseCommandHandler>();
        services.AddScoped<IRequestHandler<EncerrarCadastroFasesCommand, FluentValidation.Results.ValidationResult>, EncerrarCadastroFasesCommandHandler>();
        services.AddScoped<IRequestHandler<ReabrirCadastroFasesCommand, FluentValidation.Results.ValidationResult>, ReabrirCadastroFasesCommandHandler>();
        services.AddScoped<IRequestHandler<DefinirConfrontoCommand, FluentValidation.Results.ValidationResult>, DefinirConfrontoCommandHandler>();
        services.AddScoped<IRequestHandler<GerarTabelaJogosCommand, FluentValidation.Results.ValidationResult>, GerarTabelaJogosCommandHandler>();
        services.AddScoped<IRequestHandler<AgendarJogoCommand, FluentValidation.Results.ValidationResult>, AgendarJogoCommandHandler>();
        services.AddScoped<IRequestHandler<AgendarJogosEmLoteCommand, FluentValidation.Results.ValidationResult>, AgendarJogosEmLoteCommandHandler>();
        services.AddScoped<IRequestHandler<CriarLocalCommand, FluentValidation.Results.ValidationResult>, CriarLocalCommandHandler>();
        services.AddScoped<IRequestHandler<RegistrarEventoCommand, FluentValidation.Results.ValidationResult>, RegistrarEventoCommandHandler>();
        services.AddScoped<IRequestHandler<RegistrarSubstituicaoCommand, FluentValidation.Results.ValidationResult>, RegistrarSubstituicaoCommandHandler>();
        services.AddScoped<IRequestHandler<DesfazerUltimaSubstituicaoCommand, FluentValidation.Results.ValidationResult>, DesfazerUltimaSubstituicaoCommandHandler>();
        services.AddScoped<IRequestHandler<PrepararSumulaCommand, FluentValidation.Results.ValidationResult>, PrepararSumulaCommandHandler>();
        services.AddScoped<IRequestHandler<SalvarRelacaoCommand, FluentValidation.Results.ValidationResult>, SalvarRelacaoCommandHandler>();
        services.AddScoped<IRequestHandler<IniciarSumulaCommand, FluentValidation.Results.ValidationResult>, IniciarSumulaCommandHandler>();
        services.AddScoped<IRequestHandler<EncerrarSumulaCommand, FluentValidation.Results.ValidationResult>, EncerrarSumulaCommandHandler>();
        services.AddScoped<IRequestHandler<RegistrarWOCommand, FluentValidation.Results.ValidationResult>, RegistrarWOCommandHandler>();

        return services;
    }
}
