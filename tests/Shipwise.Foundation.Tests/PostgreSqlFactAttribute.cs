using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHIPWISE_TEST_POSTGRES")))
        {
            Skip = "Set SHIPWISE_TEST_POSTGRES to an isolated, empty PostgreSQL test database.";
        }
    }
}
