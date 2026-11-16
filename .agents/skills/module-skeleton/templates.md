# Module skeleton templates

A snapshot of the Identity module skeleton (`src/Modules/Shipwise.Identity` and
`tests/Shipwise.Identity.Tests`) as first created, with the module renamed to `Sample`
(schema `sample`). Replace `Sample` and `sample` with the new module's name and keep
everything else; add nothing business-specific.

The Identity form of these files is the one that was built under the repository's
analyzers with no warning and tested, including against PostgreSQL. The `Sample` form
below was produced from it by renaming only. If the Identity skeleton files in Git history
and this snapshot ever differ, the Identity files win and this file is regenerated.

## Project files

`src/Modules/Shipwise.Sample/Shipwise.Sample.Domain/Shipwise.Sample.Domain.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.SharedKernel/Shipwise.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Application/Shipwise.Sample.Application.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../Shipwise.Sample.Domain/Shipwise.Sample.Domain.csproj" />
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.SharedKernel/Shipwise.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Infrastructure/Shipwise.Sample.Infrastructure.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Shipwise.Sample.Application/Shipwise.Sample.Application.csproj" />
    <ProjectReference Include="../Shipwise.Sample.Domain/Shipwise.Sample.Domain.csproj" />
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.EntityFrameworkCore/Shipwise.EntityFrameworkCore.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.API/Shipwise.Sample.API.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <UserSecretsId>shipwise-sample-api</UserSecretsId>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Shipwise.Sample.Application/Shipwise.Sample.Application.csproj" />
    <ProjectReference Include="../Shipwise.Sample.Infrastructure/Shipwise.Sample.Infrastructure.csproj" />
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.AspNetCore/Shipwise.AspNetCore.csproj" />
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.Observability/Shipwise.Observability.csproj" />
  </ItemGroup>
</Project>
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.MigrationRunner/Shipwise.Sample.MigrationRunner.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Shipwise.Sample.Infrastructure/Shipwise.Sample.Infrastructure.csproj" />
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.Observability/Shipwise.Observability.csproj" />
  </ItemGroup>
</Project>
```

`tests/Shipwise.Sample.Tests/Shipwise.Sample.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Modules/Shipwise.Sample/Shipwise.Sample.API/Shipwise.Sample.API.csproj" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

## Source files

The Domain project holds the same `AssemblyReference` class in its own namespace and
nothing else.

`src/Modules/Shipwise.Sample/Shipwise.Sample.Application/AssemblyReference.cs`

```csharp
using System.Reflection;

namespace Shipwise.Sample.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Application/DependencyInjection/ServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Shipwise.SharedKernel.DependencyInjection;

namespace Shipwise.Sample.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
        => services
            .AddSelfMadeCqrs(AssemblyReference.Assembly)
            .AddApplicationPipelineBehaviors();
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shipwise.EntityFrameworkCore.Interceptors;
using Shipwise.Sample.Infrastructure.Persistence.Core;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Abstractions.Persistence;

namespace Shipwise.Sample.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "ApplicationDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddPersistence(configuration)
            .AddUnitOfWork();

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<SampleDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", SampleDbContext.Schema)
                .MigrationsAssembly(typeof(SampleDbContext).Assembly.GetName().Name))
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        => services.AddScoped<IUnitOfWork, SampleUnitOfWork>();
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Infrastructure/Persistence/DbContexts/SampleDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace Shipwise.Sample.Infrastructure.Persistence.DbContexts;

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public const string Schema = "sample";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SampleDbContext).Assembly);
    }
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Infrastructure/Persistence/DbContexts/SampleDbContextFactory.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shipwise.Sample.Infrastructure.Persistence.DbContexts;

public sealed class SampleDbContextFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("SHIPWISE_DESIGN_POSTGRES")
            ?? "Host=127.0.0.1;Port=25431;Database=shipwise_sample;Username=shipwise_tests";

        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", SampleDbContext.Schema))
            .Options;

        return new SampleDbContext(options);
    }
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.Infrastructure/Persistence/Core/SampleUnitOfWork.cs`

```csharp
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Abstractions.Persistence;
using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.Sample.Infrastructure.Persistence.Core;

public sealed class SampleUnitOfWork(SampleDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            TResponse response = await action(cancellationToken);
            if (response is Result { IsFailure: true })
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return response;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            // Disposing the uncommitted transaction rolls it back; an explicit rollback here
            // could throw and hide the original failure.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.API/Health/DatabaseHealthCheck.cs`

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;

namespace Shipwise.Sample.API.Health;

public sealed class DatabaseHealthCheck(SampleDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.API/Program.cs`

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Shipwise.AspNetCore.DependencyInjection;
using Shipwise.AspNetCore.Filters;
using Shipwise.AspNetCore.Middleware;
using Shipwise.AspNetCore.Options;
using Shipwise.Observability;
using Shipwise.Sample.API.Health;
using Shipwise.Sample.Application.DependencyInjection;
using Shipwise.Sample.Infrastructure.DependencyInjection;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    string secretsDirectory = builder.Configuration["SHIPWISE_CONFIG_SECRETS_DIRECTORY"] ?? "/run/secrets/config";
    builder.Configuration.AddKeyPerFile(Path.GetFullPath(secretsDirectory), optional: true, reloadOnChange: false);

    builder.AddConfiguredSerilog();
    builder.AddShipwiseObservability();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddCurrentUserProvider();
    builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>());
    builder.Services.AddConfiguredCors(builder.Configuration);
    builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddConfiguredAuthorization();
    builder.Services.AddTrustedForwarding(builder.Configuration);
    builder.Services.AddConfiguredApiVersioning();
    builder.Services.AddConfiguredSwagger();
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseCorrelationId();
    app.UseGlobalExceptionHandling();
    app.UseApiStatusCodeEnvelope();
    app.UseSerilogRequestLogging();
    if (app.Environment.IsDevelopment())
    {
        app.UseConfiguredSwagger();
    }

    app.UseCors(CorsOptions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthAsync,
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthAsync,
    }).AllowAnonymous();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal("Shipwise.Sample API terminated unexpectedly ({ExceptionType})", exception.GetType().Name);
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static Task WriteHealthAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() }),
    }));
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.MigrationRunner/Program.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shipwise.Observability;
using Shipwise.Sample.Infrastructure.DependencyInjection;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;

var builder = Host.CreateApplicationBuilder(args);
builder.AddShipwiseObservability();
builder.Services.AddPersistence(builder.Configuration);

using IHost host = builder.Build();
ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Shipwise.Sample.MigrationRunner");
try
{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    SampleDbContext context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    logger.LogInformation("Applying Sample database migrations");
    await context.Database.MigrateAsync();
    logger.LogInformation("Sample database is up to date");
    return 0;
}
catch (Exception exception)
{
    logger.LogCritical("Sample migration failed ({ExceptionType})", exception.GetType().Name);
    return 1;
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.API/appsettings.json`

```json
{
  "AllowedHosts": "localhost;127.0.0.1",
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ],
    "AllowedMethods": [ "GET", "POST", "PUT", "DELETE" ],
    "AllowedHeaders": [ "Content-Type", "Authorization", "Idempotency-Key", "X-Correlation-ID", "traceparent", "tracestate" ],
    "ExposedHeaders": [ "X-Correlation-ID", "Retry-After" ],
    "AllowCredentials": false
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console" ],
    "MinimumLevel": { "Default": "Information", "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore.Database.Command": "Warning" } },
    "WriteTo": [ { "Name": "Console", "Args": { "formatter": "Serilog.Formatting.Json.JsonFormatter, Serilog" } } ]
  },
  "Observability": { "Enabled": false, "ServiceName": "Shipwise.Sample.API" }
}
```

`src/Modules/Shipwise.Sample/Shipwise.Sample.API/Properties/launchSettings.json`

```json
{
  "profiles": {
    "Local": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:19091",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_ENVIRONMENT": "Development" }
    }
  }
}
```

## Test project

`Host/` needs no database; `Persistence/` runs only with `SHIPWISE_TEST_POSTGRES` set and
must run in CI. The probe controller and requests are test-only and never ship.

`tests/Shipwise.Sample.Tests/Host/SampleApiFactory.cs`

```csharp
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shipwise.SharedKernel.DependencyInjection;
using Shipwise.SharedKernel.Security;

namespace Shipwise.Sample.Tests.Host;

/// <summary>
/// Starts the real Sample API with test-only settings, plus the probe controller and
/// requests from this assembly so the pipeline can be exercised without a business endpoint.
/// </summary>
public class SampleApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789-abcdef";
    public const string Issuer = "shipwise-tests";
    public const string Audience = "shipwise-tests";

    /// <summary>Gets the connection string the host uses. The default points at nothing.</summary>
    protected virtual string ConnectionString
        => "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1";

    public static string CreateToken(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(CustomClaims.SessionId, Guid.NewGuid().ToString()),
        };
        claims.AddRange(permissions.Select(permission => new Claim(CustomClaims.Permission, permission)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The host reads configuration while it registers services, so these must be host
        // settings: values added later through ConfigureAppConfiguration arrive too late.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ApplicationDb", ConnectionString);
        builder.UseSetting("Jwt:SecretKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "5");
        builder.UseSetting("IntegrationApi:ApiKeys:0", "test-only-integration-key");
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(SampleApiFactory).Assembly);
            services.AddSelfMadeCqrs(typeof(SampleApiFactory).Assembly);
            services.AddScoped<IAuthenticatedContextValidator, AlwaysActiveValidator>();
        });
    }

    private sealed class AlwaysActiveValidator : IAuthenticatedContextValidator
    {
        public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}
```

`tests/Shipwise.Sample.Tests/Host/ProbeEndpoints.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shipwise.AspNetCore.Controllers;
using Shipwise.AspNetCore.Security;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Cqrs;
using Shipwise.SharedKernel.Exceptions;
using Shipwise.SharedKernel.Security;
using ValidationException = Shipwise.SharedKernel.Exceptions.ValidationException;

namespace Shipwise.Sample.Tests.Host;

/// <summary>
/// Test-only endpoints. They exist so the skeleton's request pipeline, authentication and
/// transaction handling can be proven before the module has any business endpoint.
/// </summary>
#pragma warning disable SA1402 // The probe types belong together and never ship.
public sealed class ProbeController(ISender sender) : ApiController(sender)
{
    [HttpGet("ok")]
    [PermissionAuthorize("Probe", ActionType.View)]
    public async Task<IActionResult> GetOk(CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new GetProbeQuery(false), cancellationToken)).Value);

    [HttpGet("missing")]
    [PermissionAuthorize("Probe", ActionType.View)]
    public async Task<IActionResult> GetMissing(CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new GetProbeQuery(true), cancellationToken)).Value);

    [HttpPost]
    [PermissionAuthorize("Probe", ActionType.Create)]
    public async Task<IActionResult> Create(CreateProbeRequest request, CancellationToken cancellationToken)
    {
        Result<string> result = await Sender.SendAsync(new CreateProbeCommand(request.Name), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }
}

public sealed record CreateProbeRequest(string Name);

public sealed record GetProbeQuery(bool Missing) : IQuery<string>;

public sealed class GetProbeQueryHandler : IQueryHandler<GetProbeQuery, string>
{
    public Task<Result<string>> HandleAsync(GetProbeQuery request, CancellationToken cancellationToken)
        => request.Missing
            ? throw new NotFoundException("Probe", 42)
            : Task.FromResult(Result.Success("hello"));
}

/// <summary>
/// Inserts a row into the probe table. The names "throw" and "fail" make the handler throw
/// or return a failed result after the insert, to prove the transaction is rolled back.
/// </summary>
public sealed record CreateProbeCommand(string Name) : ICommand<string>;

public sealed class CreateProbeCommandValidator : IValidator<CreateProbeCommand>
{
    public Task ValidateAsync(CreateProbeCommand request, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(request.Name)
            ? throw new ValidationException([new ValidationError("name", "Name is required.")])
            : Task.CompletedTask;
}

public sealed class CreateProbeCommandHandler(SampleDbContext dbContext) : ICommandHandler<CreateProbeCommand, string>
{
    public async Task<Result<string>> HandleAsync(CreateProbeCommand request, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"INSERT INTO public.\"SkeletonProbe\" (\"Name\") VALUES ({request.Name})",
            cancellationToken);

        return request.Name switch
        {
            "throw" => throw new ConflictException("Probe.Conflict", "The probe was asked to throw."),
            "fail" => Result.Failure<string>(new Error("Probe.Failed", "The probe was asked to fail.")),
            _ => Result.Success(request.Name),
        };
    }
}
#pragma warning restore SA1402
```

`tests/Shipwise.Sample.Tests/Host/HostTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Shipwise.Sample.Tests.Host;

/// <summary>Host behavior that needs no database.</summary>
public sealed class HostTests(SampleApiFactory factory) : IClassFixture<SampleApiFactory>
{
    [Fact]
    public async Task LiveIsHealthyWithoutCredentials()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyIsUnhealthyWhenTheDatabaseIsUnreachable()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ControllerWithoutItsOwnAttributeRejectsMissingToken()
    {
        // ProbeController carries no [Authorize]; the ApiController base class must supply it.
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/v1/probe/ok");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Request.Unauthorized", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenWithoutThePermissionIsForbidden()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.CreateToken()).GetAsync("/api/v1/probe/ok");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QueryRunsThroughThePipelineWithThePermission()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.CreateToken("Probe.View")).GetAsync("/api/v1/probe/ok");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("\"data\":\"hello\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotFoundExceptionBecomes404WithItsCode()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.CreateToken("Probe.View")).GetAsync("/api/v1/probe/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Probe.NotFound", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCommandReturnsFieldErrorsBeforeAnyTransaction()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.CreateToken("Probe.Create"))
            .PostAsJsonAsync("/api/v1/probe", new { name = string.Empty });
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Contains("\"name\":[\"Name is required.\"]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownApiRouteAnswersWithTheErrorEnvelope()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Request.NotFound", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public void ModelMatchesTheMigrationsAndUsesTheModuleSchema()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SampleDbContext context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(SampleDbContext.Schema, context.Model.GetDefaultSchema());
        Assert.Equal("sample", SampleDbContext.Schema);
    }

    private HttpClient Client(string token)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
```

`tests/Shipwise.Sample.Tests/Persistence/PostgreSqlFactAttribute.cs`

```csharp
using Xunit;

namespace Shipwise.Sample.Tests.Persistence;

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
```

`tests/Shipwise.Sample.Tests/Persistence/PostgreSqlDatabaseFixture.cs`

```csharp
using Npgsql;
using Xunit;

namespace Shipwise.Sample.Tests.Persistence;

/// <summary>
/// Creates a database of its own on the configured test server and drops it afterwards, so
/// these tests never share tables with another test project running at the same time.
/// Does nothing when no server is configured; the tests are skipped in that case.
/// </summary>
public sealed class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    private readonly string? _serverConnectionString =
        Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ServerVariable);

    private readonly string _databaseName = "shipwise_sample_tests_" + Guid.NewGuid().ToString("N");

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
```

`tests/Shipwise.Sample.Tests/Persistence/PersistenceTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shipwise.Sample.Infrastructure.DependencyInjection;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;
using Shipwise.Sample.Tests.Host;
using Xunit;

namespace Shipwise.Sample.Tests.Persistence;

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
        SampleDbContext context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();

        long tables = await database.ScalarAsync(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = @schema AND table_name = '__EFMigrationsHistory'",
            new NpgsqlParameter("schema", SampleDbContext.Schema));
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
            new AuthenticationHeaderValue("Bearer", SampleApiFactory.CreateToken("Probe.Create"));

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

    private sealed class DatabaseApiFactory(string connectionString) : SampleApiFactory
    {
        protected override string ConnectionString => connectionString;
    }
}
```
