using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shipwise.Identity.Infrastructure.DependencyInjection;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Shipwise.Identity.Tests.Registration;

public sealed class DatabasePrivacyTests
{
    [Fact]
    public void PersistenceDisablesDatabaseErrorDetailsEvenWhenConfigured()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Database=test;Username=test;Include Error Detail=true",
        }).Build();
        using var provider = new ServiceCollection().AddLogging().AddPersistence(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();
        string connection = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.GetConnectionString()!;
        Assert.False(new NpgsqlConnectionStringBuilder(connection).IncludeErrorDetail);
    }
}
