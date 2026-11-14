using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.SharedKernel.Cqrs;

public interface IPublisher
{
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
