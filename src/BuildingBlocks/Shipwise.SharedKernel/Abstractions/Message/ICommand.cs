using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.SharedKernel.Abstractions.Message;

public interface ICommand : IRequest<Result> { }

public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }
