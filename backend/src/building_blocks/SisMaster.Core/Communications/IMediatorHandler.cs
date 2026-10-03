using FluentValidation.Results;
using SisMaster.Core.Messages;
using SisMaster.Core.Messages.CommonMessages.DomainEvents;
using SisMaster.Core.Messages.CommonMessages.Notifications;

namespace SisMaster.Core.Communications
{
    public interface IMediatorHandler
    {
        Task PublicarEvento<T>(T evento) where T : Event;
        Task PublicarNotificacao<T>(T notificacao) where T : DomainNotification;
        Task PublicarDomainEvent<T>(T notificacao) where T : DomainEvent;
        Task<ValidationResult> EnviarComando<T>(T comando) where T : Command;
    }
}
