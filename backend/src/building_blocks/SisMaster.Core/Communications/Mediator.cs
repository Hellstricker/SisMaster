using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SisMaster.Core.Communications
{
    // Mediador próprio do sistema (substitui o pacote MediatR, que passou a exigir licença comercial). Só faz o que o sistema usa:
    // enviar um comando a UM manipulador e publicar uma notificação a TODOS os manipuladores registrados, um após o outro.

    /// <summary>Pedido (comando) que tem exatamente um manipulador e devolve <typeparamref name="TResponse"/>.</summary>
    public interface IRequest<out TResponse> { }

    public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>Mensagem publicada a zero ou mais manipuladores (eventos e notificações de domínio).</summary>
    public interface INotification { }

    public interface INotificationHandler<in TNotification> where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken);
    }

    public interface IMediator
    {
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
    }

    public sealed class Mediator : IMediator
    {
        private static readonly ConcurrentDictionary<Type, MethodInfo> Metodos = new();
        private readonly IServiceProvider _servicos;

        public Mediator(IServiceProvider servicos) => _servicos = servicos;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var tipoDoManipulador = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
            var manipulador = _servicos.GetService(tipoDoManipulador)
                ?? throw new InvalidOperationException($"Nenhum manipulador registrado para {request.GetType().Name}.");
            return await Invocar<TResponse>(tipoDoManipulador, manipulador, request, cancellationToken);
        }

        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            ArgumentNullException.ThrowIfNull(notification);
            // Os manipuladores do tipo da notificação e dos tipos dos quais ela herda, na ordem de registro, um de cada vez.
            for (var tipo = notification.GetType(); tipo is not null && typeof(INotification).IsAssignableFrom(tipo); tipo = tipo.BaseType)
            {
                var tipoDoManipulador = typeof(INotificationHandler<>).MakeGenericType(tipo);
                foreach (var manipulador in _servicos.GetServices(tipoDoManipulador))
                    if (manipulador is not null)
                        await Invocar(tipoDoManipulador, manipulador, notification, cancellationToken);
            }
        }

        private static MethodInfo Metodo(Type tipoDoManipulador) => Metodos.GetOrAdd(tipoDoManipulador, t => t.GetMethod("Handle")!);

        private static async Task<TResponse> Invocar<TResponse>(Type tipoDoManipulador, object manipulador, object mensagem, CancellationToken ct)
        {
            try { return await (Task<TResponse>)Metodo(tipoDoManipulador).Invoke(manipulador, [mensagem, ct])!; }
            catch (TargetInvocationException e) when (e.InnerException is not null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        private static async Task Invocar(Type tipoDoManipulador, object manipulador, object mensagem, CancellationToken ct)
        {
            try { await (Task)Metodo(tipoDoManipulador).Invoke(manipulador, [mensagem, ct])!; }
            catch (TargetInvocationException e) when (e.InnerException is not null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
    }

    public static class MediatorExtensions
    {
        /// <summary>Registra o mediador e todos os manipuladores (de comandos e de notificações) encontrados nos assemblies informados.</summary>
        public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
        {
            services.TryAddTransient<IMediator, Mediator>();
            foreach (var tipo in assemblies.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
                foreach (var contrato in tipo.GetInterfaces().Where(i => i.IsGenericType))
                {
                    var definicao = contrato.GetGenericTypeDefinition();
                    if (definicao == typeof(IRequestHandler<,>) || definicao == typeof(INotificationHandler<>))
                        services.TryAddEnumerable(ServiceDescriptor.Transient(contrato, tipo));
                }
            return services;
        }
    }
}
