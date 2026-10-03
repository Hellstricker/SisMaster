using MediatR;
using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Communications;
using SisMaster.Core.Messages.CommonMessages.Notifications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Commands.Handlers;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Data.Repositories;
using SisMaster.WebApps.WebApi.Domain.Partida;
using SisMaster.WebApps.WebApi.Domain.Time;

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
        services.AddDbContext<CampeonatoDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("CampeonatoConnection")));

        // Repositories
        services.AddScoped<ITimeRepository, TimeRepository>();
        services.AddScoped<IPartidaRepository, PartidaRepository>();

        // Command Handlers
        services.AddScoped<IRequestHandler<CriarTimeCommand, FluentValidation.Results.ValidationResult>, CriarTimeCommandHandler>();
        services.AddScoped<IRequestHandler<AdicionarJogadorCommand, FluentValidation.Results.ValidationResult>, AdicionarJogadorCommandHandler>();
        services.AddScoped<IRequestHandler<CriarPartidaCommand, FluentValidation.Results.ValidationResult>, CriarPartidaCommandHandler>();
        services.AddScoped<IRequestHandler<RegistrarEventoCommand, FluentValidation.Results.ValidationResult>, RegistrarEventoCommandHandler>();

        return services;
    }
}
