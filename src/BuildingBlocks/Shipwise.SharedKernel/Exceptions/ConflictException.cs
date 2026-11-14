using Shipwise.SharedKernel.Abstractions.Exceptions;

namespace Shipwise.SharedKernel.Exceptions;

public class ConflictException : DomainException
{
    public ConflictException(string code, string message)
        : base(code, message) { }

    public ConflictException(string message)
        : this("Conflict", message) { }
}
