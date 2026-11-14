using Shipwise.SharedKernel.Abstractions.Exceptions;

namespace Shipwise.SharedKernel.Exceptions;

public class BadRequestException : DomainException
{
    public BadRequestException(string code, string message)
        : base(code, message) { }

    public BadRequestException(string message)
        : this("BadRequest", message) { }
}
