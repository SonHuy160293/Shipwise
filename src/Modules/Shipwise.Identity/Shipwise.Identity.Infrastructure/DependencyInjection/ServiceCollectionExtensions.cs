using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Shipwise.EntityFrameworkCore.Interceptors;
using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Infrastructure.Persistence.Core;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.Identity.Infrastructure.Persistence.Repositories;
using Shipwise.Identity.Infrastructure.Security;
using Shipwise.SharedKernel.Abstractions.Persistence;

namespace Shipwise.Identity.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "ApplicationDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration).AddUnitOfWork();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordCredentialRepository, PasswordCredentialRepository>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");
        }

        // PostgreSQL error details can contain email addresses or password hashes.
        var safeConnection = new NpgsqlConnectionStringBuilder(connectionString) { IncludeErrorDetail = false };

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<IdentityDbContext>((serviceProvider, options) => options
            .UseNpgsql(safeConnection.ConnectionString, npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name))
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        => services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();
}
