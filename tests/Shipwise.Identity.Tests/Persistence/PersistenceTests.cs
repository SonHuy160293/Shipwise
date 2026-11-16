using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shipwise.Identity.Infrastructure.DependencyInjection;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.Identity.Tests.Host;
using Xunit;

namespace Shipwise.Identity.Tests.Persistence;

/// <summary>Behavior that needs a real PostgreSQL database.</summary>
public sealed class PersistenceTests(PostgreSqlDatabaseFixture database) : IClassFixture<PostgreSqlDatabaseFixture>
{
    [PostgreSqlFact]
    public async Task MigrateCreatesTheHistoryTableInTheModuleSchemaAndCanRunTwice()
    {
        // The MigrationRunner does exactly this: AddPersistence, then MigrateAsync.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:" + ServiceCollectionExtensions.ConnectionStringName] = database.ConnectionString,
            })
            .Build();
        await using ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddPersistence(configuration)
            .BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();

        long tables = await database.ScalarAsync(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = @schema AND table_name = '__EFMigrationsHistory'",
            new NpgsqlParameter("schema", IdentityDbContext.Schema));
        Assert.Equal(1, tables);
    }

    [PostgreSqlFact]
    public async Task ReadyIsHealthyWhenTheDatabaseIsReachable()
    {
        await using var factory = new DatabaseApiFactory(database.ConnectionString);

        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgreSqlFact]
    public async Task CommandCommitsOnSuccessAndLeavesNothingBehindOnFailure()
    {
        await database.ExecuteAsync("CREATE TABLE IF NOT EXISTS public.\"SkeletonProbe\" (\"Name\" text NOT NULL)");
        await using var factory = new DatabaseApiFactory(database.ConnectionString);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IdentityApiFactory.CreateToken("Probe.Create"));

        HttpResponseMessage committed = await client.PostAsJsonAsync("/api/v1/probe", new { name = "kept" });
        HttpResponseMessage threw = await client.PostAsJsonAsync("/api/v1/probe", new { name = "throw" });
        HttpResponseMessage failed = await client.PostAsJsonAsync("/api/v1/probe", new { name = "fail" });

        Assert.True(committed.StatusCode == HttpStatusCode.OK, await committed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, threw.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Equal(1, await CountAsync("kept"));
        Assert.Equal(0, await CountAsync("throw"));
        Assert.Equal(0, await CountAsync("fail"));
    }

    private Task<long> CountAsync(string name)
        => database.ScalarAsync(
            "SELECT count(*) FROM public.\"SkeletonProbe\" WHERE \"Name\" = @name",
            new NpgsqlParameter("name", name));

    private sealed class DatabaseApiFactory(string connectionString) : IdentityApiFactory
    {
        protected override string ConnectionString => connectionString;
    }
}
