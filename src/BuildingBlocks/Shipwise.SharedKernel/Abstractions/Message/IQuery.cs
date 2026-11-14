using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.SharedKernel.Abstractions.Message;

public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
