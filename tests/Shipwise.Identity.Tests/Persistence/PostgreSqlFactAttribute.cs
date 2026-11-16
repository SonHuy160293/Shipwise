using Xunit;

namespace Shipwise.Identity.Tests.Persistence;

/// <summary>Runs only when a PostgreSQL test server is configured; CI always configures one.</summary>
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public const string ServerVariable = "SHIPWISE_TEST_POSTGRES";

    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ServerVariable)))
        {
            Skip = $"Set {ServerVariable} to a PostgreSQL test server whose user may create databases.";
        }
    }
}
