using EasyNetQ;
using SisMaster.Core.Messages.Integrations;

namespace SisMaster.MessageBus
{
    public interface IMessageBus
    {
        bool IsConnected { get; }
        IAdvancedBus AdvancedBus { get; }

        Task PublishAsync<T>(T message) where T : IntegrationEvent;

        void SubscribeAsync<T>(string subscriptionId, Func<T, Task> onMessage) where T : class;        

        Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage;        

        IAsyncDisposable RespondAsync<TRequest, TResponse>(Func<TRequest, Task<TResponse>> responder)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage;
    }
}
