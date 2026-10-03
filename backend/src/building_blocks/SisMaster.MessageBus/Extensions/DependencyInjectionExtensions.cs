using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SisMaster.MessageBus.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddMessageBus(this IServiceCollection services, IConfiguration configuration)
        {
            var conn = configuration.GetConnectionString("RabbitMQ")
                ?? throw new InvalidOperationException("Connection string 'RabbitMQ' não configurada");

            services.AddEasyNetQ(conn);
            services.AddSingleton<IMessageBus, MessageBus>();

            return services;
        }
    }
}
