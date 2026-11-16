# Module skeleton templates

These files are the throwaway module `Sample` (schema `sample`) that was built and run to
verify the module-skeleton rules. Replace `Sample` with the module name and `sample` with
the lowercase schema, keep everything else, and add nothing business-specific.

The source files below are copied unchanged from that run. The project files are shown in
repository form: the run used the same references with explicit versions, because it was
built outside the repository.

## Project files

`Shipwise.Sample.Domain.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.SharedKernel/Shipwise.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`Shipwise.Sample.Application.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../Shipwise.Sample.Domain/Shipwise.Sample.Domain.csproj" />
    <ProjectReference Include="../../../BuildingBlocks/Shipwise.SharedKernel/Shipwise.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`Shipwise.Sample.Infrastructure.csproj`

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

`Shipwise.Sample.API.csproj`

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

`Shipwise.Sample.MigrationRunner.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <UserSecretsId>shipwise-sample-migrationrunner</UserSecretsId>
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

The Domain project holds the same `AssemblyReference` class in the
`Shipwise.Sample.Domain` namespace and nothing else.

`Shipwise.Sample.Application/AssemblyReference.cs`

```csharp
using System.Reflection;

namespace Shipwise.Sample.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
```

`Shipwise.Sample.Application/DependencyInjection/ServiceCollectionExtensions.cs`

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

`Shipwise.Sample.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

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

`Shipwise.Sample.Infrastructure/Persistence/DbContexts/SampleDbContext.cs`

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

`Shipwise.Sample.Infrastructure/Persistence/DbContexts/SampleDbContextFactory.cs`

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

`Shipwise.Sample.Infrastructure/Persistence/Core/SampleUnitOfWork.cs`

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
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // The original failure is the one worth reporting.
            }

            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
```

`Shipwise.Sample.API/Health/DatabaseHealthCheck.cs`

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

`Shipwise.Sample.API/Program.cs`

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

`Shipwise.Sample.MigrationRunner/Program.cs`

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

## Test project

The factory, a token helper and the test-only controller, query, command and validator that
exercise the pipeline. Keep the test-only types in the test project.

`Shipwise.Sample.Tests/HostTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shipwise.AspNetCore.Controllers;
using Shipwise.AspNetCore.Security;
using Shipwise.Sample.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Cqrs;
using Shipwise.SharedKernel.DependencyInjection;
using Shipwise.SharedKernel.Exceptions;
using Shipwise.SharedKernel.Security;
using Xunit;
using ValidationException = Shipwise.SharedKernel.Exceptions.ValidationException;

namespace Shipwise.Sample.Tests;

public sealed class SampleApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789-abcdef";
    public const string Issuer = "shipwise-tests";
    public const string Audience = "shipwise-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ApplicationDb", "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1");
        builder.UseSetting("Jwt:SecretKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "5");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        builder.UseSetting("Cors:AllowedMethods:0", "GET");
        builder.UseSetting("Cors:AllowedHeaders:0", "content-type");
        builder.UseSetting("IntegrationApi:ApiKeys:0", "test-only-integration-key");
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(SampleApiFactory).Assembly);
            services.AddSelfMadeCqrs(typeof(SampleApiFactory).Assembly);
            services.AddScoped<IAuthenticatedContextValidator, AlwaysActiveValidator>();
        });
    }

    public static string Token(params string[] permissions)
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
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed class AlwaysActiveValidator : IAuthenticatedContextValidator
    {
        public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}

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
    public async Task ProtectedEndpointRejectsMissingToken()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/v1/probe/ok");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Request.Unauthorized", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProtectedEndpointRejectsTokenWithoutPermission()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.Token()).GetAsync("/api/v1/probe/ok");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QueryRunsThroughThePipelineWithPermission()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.Token("Probe.View")).GetAsync("/api/v1/probe/ok");
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("\"data\":\"hello\"", body);
    }

    [Fact]
    public async Task NotFoundExceptionBecomes404()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.Token("Probe.View")).GetAsync("/api/v1/probe/missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Thing.NotFound", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InvalidCommandReturnsFieldErrorsBeforeAnyTransaction()
    {
        HttpResponseMessage response = await Client(SampleApiFactory.Token("Probe.Create"))
            .PostAsJsonAsync("/api/v1/probe", new { name = string.Empty });
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Contains("\"name\":[\"Name is required.\"]", body);
    }

    [Fact]
    public void ModelHasNoPendingChanges()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SampleDbContext context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal("sample", context.Model.GetDefaultSchema());
    }

    private HttpClient Client(string token)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed record GetThingQuery(bool Missing) : IQuery<string>;

public sealed class GetThingQueryHandler : IQueryHandler<GetThingQuery, string>
{
    public Task<Result<string>> HandleAsync(GetThingQuery request, CancellationToken cancellationToken)
        => request.Missing ? throw new NotFoundException("Thing", 42) : Task.FromResult(Result.Success("hello"));
}

public sealed record CreateThingRequest(string Name);

public sealed record CreateThingCommand(string Name) : ICommand<Guid>;

public sealed class CreateThingCommandValidator : IValidator<CreateThingCommand>
{
    public Task ValidateAsync(CreateThingCommand request, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(request.Name)
            ? throw new ValidationException([new ValidationError("name", "Name is required.")])
            : Task.CompletedTask;
}

public sealed class CreateThingCommandHandler : ICommandHandler<CreateThingCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(CreateThingCommand request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(Guid.NewGuid()));
}

[Authorize]
public sealed class ProbeController(ISender sender) : ApiController(sender)
{
    [HttpGet("ok")]
    [PermissionAuthorize("Probe", ActionType.View)]
    public async Task<IActionResult> GetOk(CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new GetThingQuery(false), cancellationToken)).Value);

    [HttpGet("missing")]
    [PermissionAuthorize("Probe", ActionType.View)]
    public async Task<IActionResult> GetMissing(CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new GetThingQuery(true), cancellationToken)).Value);

    [HttpPost]
    [PermissionAuthorize("Probe", ActionType.Create)]
    public async Task<IActionResult> Create(CreateThingRequest request, CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new CreateThingCommand(request.Name), cancellationToken)).Value);
}
```
