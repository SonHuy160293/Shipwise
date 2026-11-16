using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shipwise.Observability;
using Shipwise.Identity.Infrastructure.DependencyInjection;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;

var builder = Host.CreateApplicationBuilder(args);
builder.AddShipwiseObservability();
builder.Services.AddPersistence(builder.Configuration);

using IHost host = builder.Build();
ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Shipwise.Identity.MigrationRunner");
try
{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    logger.LogInformation("Applying Identity database migrations");
    await context.Database.MigrateAsync();
    logger.LogInformation("Identity database is up to date");
    return 0;
}
catch (Exception exception)
{
    logger.LogCritical("Identity migration failed ({ExceptionType})", exception.GetType().Name);
    return 1;
}
