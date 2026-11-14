using Shipwise.SharedKernel.Abstractions.Exceptions;

namespace Shipwise.SharedKernel.Exceptions;

public class ForbiddenException : DomainException
{
    public ForbiddenException()
        : this("Access denied.")
    {
    }

    public ForbiddenException(string message)
        : base("Forbidden", message)
    {
    }

    public ForbiddenException(string code, string message)
        : base(code, message)
    {
    }
}
