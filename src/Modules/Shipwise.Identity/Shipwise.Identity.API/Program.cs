using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Shipwise.AspNetCore.DependencyInjection;
using Shipwise.AspNetCore.Filters;
using Shipwise.AspNetCore.Middleware;
using Shipwise.AspNetCore.Options;
using Shipwise.Observability;
using Shipwise.Identity.API.Health;
using Shipwise.Identity.API.Configuration;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.Identity.Application.DependencyInjection;
using Shipwise.Identity.Infrastructure.DependencyInjection;

// A plain logger, not CreateBootstrapLogger(): a reloadable bootstrap logger can be frozen
// by only one host per process, so hosts started in parallel (tests) fail with
// "The logger is already frozen". AddConfiguredSerilog builds the host's own logger.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

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
    builder.Services.AddSingleton<AuthCookieWriter>();
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiResponseEnvelopeFilter>();
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    }).ConfigureApiBehaviorOptions(options =>
    {
        var defaultResponse = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            if (context.ActionDescriptor is ControllerActionDescriptor
                { ControllerTypeInfo.Name: "AuthController", ActionName: "SignIn" })
            {
                // JSON parser messages can contain attacker-controlled text. Return only public field names.
                var errors = new Dictionary<string, string[]>
                {
                    ["email"] = ["Provide an email."],
                    ["password"] = ["Provide a password."],
                };
                var error = new ApiErrorResponse("Validation.Failure", "The sign-in request is invalid.", errors, context.HttpContext.TraceIdentifier);
                return new BadRequestObjectResult(error);
            }

            return defaultResponse(context);
        };
    });
    builder.Services.AddConfiguredCors(builder.Configuration);
    builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddConfiguredAuthorization();
    builder.Services.AddTrustedForwarding(builder.Configuration);
    builder.Services.AddConfiguredApiVersioning();
    builder.Services.AddConfiguredSwagger("Shipwise API");
    builder.Services.Configure<RegistrationOptions>(builder.Configuration.GetSection("Registration"));
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("registration", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.AddPolicy("sign-in", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan delay) ? delay : TimeSpan.FromMinutes(1);
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            var error = new ApiErrorResponse("Request.TooManyRequests", "Too many requests.", TraceId: context.HttpContext.TraceIdentifier);
            await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(error), cancellationToken);
        };
    });
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
    app.UseRouting();
    app.UseMiddleware<OriginCheckMiddleware>();
    app.Use(async (context, next) =>
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "registration"
            && !context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<RegistrationOptions>>().CurrentValue.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
                "Request.NotFound", "The requested resource was not found.", TraceId: context.TraceIdentifier)));
            return;
        }

        await next(context);
    });
    app.UseRateLimiter();
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
    Log.Fatal("Shipwise.Identity API terminated unexpectedly ({ExceptionType})", exception.GetType().Name);
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
