using EasyNetQ;
using SisMaster.Core.Messages.Integrations;

namespace SisMaster.MessageBus
{
    public class MessageBus : IMessageBus
    {
        private readonly IBus? _bus;
        private readonly IAdvancedBus? _advancedBus;

        public bool IsConnected => _bus != null;

        public IAdvancedBus AdvancedBus => _bus!.Advanced;

        public MessageBus(IBus bus)
        {
            _bus = bus;
            _advancedBus = _bus.Advanced;
        }        

        public async Task PublishAsync<T>(T message) where T : IntegrationEvent
        {
         
            await _bus!.PubSub.PublishAsync(message);
        }

        public void SubscribeAsync<T>(string subscriptionId, Func<T, Task> onMessage) where T : class
        {
         
            _bus!.PubSub.SubscribeAsync(subscriptionId, onMessage);
        }

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage
        {
         
            return await _bus!.Rpc.RequestAsync<TRequest, TResponse>(request);
        }

        public IAsyncDisposable RespondAsync<TRequest, TResponse>(Func<TRequest, Task<TResponse>> responder)
            where TRequest : IntegrationEvent
            where TResponse : ResponseMessage
        {
            return _bus!.Rpc.RespondAsync(responder).GetAwaiter().GetResult();
        }
    }
}
