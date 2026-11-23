using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Shipwise.Identity.Tests.Persistence;

public sealed class BootstrapRoleTests
{
    [DockerPostgreSqlFact]
    public async Task BootstrapCanRunTwiceAndGrantsDataAccessOnlyOnFutureMigratorTables()
    {
        // Bootstrap uses fixed production role/database names. Exercise the shipped scripts
        // on a fresh server so a test can never adopt or drop an existing role or database.
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string template = Path.Combine(root, "deploy/env/local.env.example");
        const string key = "SHIPWISE_POSTGRES_IMAGE=";
        string image = File.ReadLines(template).Single(line => line.StartsWith(key, StringComparison.Ordinal))[key.Length..];
        string name = $"shipwise-role-test-{Guid.NewGuid():N}";
        const string initialize = """
            set -eu
            umask 077
            for name in postgres-admin-password identity-migrator-password identity-api-password; do
                head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n' > "/run/secrets/$name"
            done
            umask 022
            exec docker-entrypoint.sh postgres
            """;

        string[] create =
        [
            "create", "--name", name, "--publish", "127.0.0.1::5432",
            "--tmpfs", "/var/lib/postgresql", "--tmpfs", "/run/secrets",
            "--mount", $"type=bind,source={Path.Combine(root, "deploy/compose")},target=/bootstrap,readonly",
            "--env", "POSTGRES_USER=shipwise_admin", "--env", "POSTGRES_DB=postgres",
            "--env", "POSTGRES_PASSWORD_FILE=/run/secrets/postgres-admin-password",
            image, "sh", "-ec", initialize,
        ];
        await DockerAsync(create);
        try
        {
            await DockerAsync("start", name);
            const string waitUntilReady = """
                for i in $(seq 1 60); do
                    if pg_isready -q -h 127.0.0.1 -U shipwise_admin -d postgres; then exit 0; fi
                    sleep 1
                done
                exit 1
                """;
            await DockerAsync("exec", name, "sh", "-ec", waitUntilReady);
            await DockerAsync("exec", "--env", "PGHOST=127.0.0.1", name, "sh", "/bootstrap/bootstrap.sh");
            await DockerAsync("exec", "--env", "PGHOST=127.0.0.1", name, "sh", "/bootstrap/bootstrap.sh");

            string address = await DockerAsync("port", name, "5432/tcp");
            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1",
                Port = int.Parse(address.Split(':')[1], CultureInfo.InvariantCulture),
                Database = "shipwise_identity",
                Pooling = false,
                Username = "identity_migrator",
                Password = await DockerAsync("exec", name, "cat", "/run/secrets/identity-migrator-password"),
            };
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(connection.ConnectionString, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema));
            await using var context = new IdentityDbContext(options.Options);
            await context.Database.MigrateAsync();

            connection.Username = "identity_api";
            connection.Password = await DockerAsync("exec", name, "cat", "/run/secrets/identity-api-password");
            await using var api = new NpgsqlConnection(connection.ConnectionString);
            await api.OpenAsync();
            const string insertUser = """
                INSERT INTO identity."Users" ("Id", "Email", "NormalizedEmail", "DisplayName", "CreatedAt")
                VALUES (gen_random_uuid(), 'test@example.invalid', 'TEST@EXAMPLE.INVALID', 'Role test', now())
                """;
            await using var insert = new NpgsqlCommand(insertUser, api);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            await using var read = new NpgsqlCommand("""SELECT "DisplayName" FROM identity."Users" """, api);
            Assert.Equal("Role test", await read.ExecuteScalarAsync());
            foreach (string ddl in new[] { "CREATE TABLE identity.denied (id integer)", """ALTER TABLE identity."Users" ADD COLUMN denied int""", """DROP TABLE identity."PasswordCredentials" """, "CREATE TEMP TABLE denied (id integer)" })
            {
                await using var command = new NpgsqlCommand(ddl, api);
                var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
            }
        }
        finally
        {
            await DockerAsync("rm", "--force", "--volumes", name);
        }
    }

    private static async Task<string> DockerAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument.ReplaceLineEndings("\n"));
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, errors);
        if (process.ExitCode != 0)
        {
            // Container output could include credentials. Report only the operation and exit code.
            throw new InvalidOperationException($"Docker {arguments[0]} failed with exit code {process.ExitCode}.");
        }

        return (await output).Trim();
    }
}
