using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shipwise.Identity.Infrastructure.Persistence.DbContexts;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("SHIPWISE_DESIGN_POSTGRES")
            ?? "Host=127.0.0.1;Port=25431;Database=shipwise_identity;Username=shipwise_tests";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options;

        return new IdentityDbContext(options);
    }
}
