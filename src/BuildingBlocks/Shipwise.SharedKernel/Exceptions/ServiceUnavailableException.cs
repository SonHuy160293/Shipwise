using Shipwise.SharedKernel.Abstractions.Exceptions;

namespace Shipwise.SharedKernel.Exceptions;

public sealed class ServiceUnavailableException : DomainException
{
    public ServiceUnavailableException(string code, string message)
        : base(code, message)
    {
    }
}
