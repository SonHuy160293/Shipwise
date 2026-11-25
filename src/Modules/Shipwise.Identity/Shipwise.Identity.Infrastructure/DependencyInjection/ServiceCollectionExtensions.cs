using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shipwise.EntityFrameworkCore.Interceptors;
using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.Core;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.Identity.Infrastructure.Persistence.Repositories;
using Shipwise.Identity.Infrastructure.Security;
using Shipwise.SharedKernel.Abstractions.Persistence;
using Shipwise.SharedKernel.Security;

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
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IAuthenticatedContextValidator, SessionContextValidator>();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenCodec, RefreshTokenCodec>();
        services.AddMemoryCache();
        services.AddSingleton<ISignInThrottle, MemorySignInThrottle>();
        services.AddOptions<SessionOptions>().Bind(configuration.GetSection("Session"))
            .Validate(
                o => o.IdleTimeoutDays > 0 && o.AbsoluteTimeoutDays > 0 && o.RefreshGraceSeconds > 0,
                "Session lifetimes must be positive.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SessionOptions>>().Value);
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
        string databaseConnection = safeConnection.ConnectionString;
        if (configuration["Database:PasswordFile"] is string passwordFile)
        {
            safeConnection.Remove("Password");

            // Explicit quoting preserves a remaining trailing LF across connection-string parsing.
            string escapedPassword = ReadPasswordFile(passwordFile).Replace("\"", "\"\"", StringComparison.Ordinal);
            databaseConnection = $"{safeConnection.ConnectionString};Password=\"{escapedPassword}\"";
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<IdentityDbContext>((serviceProvider, options) => options
            .UseNpgsql(databaseConnection, npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name))
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        => services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();

    private static string ReadPasswordFile(string path)
    {
        string password = File.ReadAllText(path);
        if (password.EndsWith("\r\n", StringComparison.Ordinal))
        {
            password = password[..^2];
        }
        else if (password.EndsWith('\n') || password.EndsWith('\r'))
        {
            password = password[..^1];
        }

        return password.Length == 0
            ? throw new InvalidOperationException("Database:PasswordFile contains an empty password.")
            : password;
    }
}
