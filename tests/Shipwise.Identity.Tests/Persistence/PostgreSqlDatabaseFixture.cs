using Npgsql;
using Xunit;

namespace Shipwise.Identity.Tests.Persistence;

/// <summary>
/// Creates a database of its own on the configured test server and drops it afterwards, so
/// these tests never share tables with another test project running at the same time.
/// Does nothing when no server is configured; the tests are skipped in that case.
/// </summary>
public sealed class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    private readonly string? _serverConnectionString =
        Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ServerVariable);

    private readonly string _databaseName = "shipwise_identity_tests_" + Guid.NewGuid().ToString("N");

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            return;
        }

        await ExecuteOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _databaseName,
            Pooling = false,
        }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            return;
        }

        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    public async Task<long> ScalarAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
