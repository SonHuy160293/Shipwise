using Npgsql;
using Shipwise.SharedKernel.Exceptions;

namespace Shipwise.Identity.Infrastructure.Persistence.Core;

public static class ConstraintExceptionMapper
{
    public static Exception? Map(PostgresException exception)
        => exception.SqlState == PostgresErrorCodes.UniqueViolation
           && exception.ConstraintName == "UX_Users_NormalizedEmail"
            ? new ConflictException("Registration.EmailTaken", "An account with this email already exists.")
            : null;
}
