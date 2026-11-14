using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.SharedKernel.Abstractions.Message;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }
