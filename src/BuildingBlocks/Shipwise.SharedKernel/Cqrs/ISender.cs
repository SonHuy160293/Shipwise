using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.SharedKernel.Cqrs;

public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
