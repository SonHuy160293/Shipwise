using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shipwise.Identity.Tests.Registration;

public sealed class RegistrationHostFactory(string? connectionString = null, bool enabled = true) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ApplicationDb", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1");
        builder.UseSetting("Jwt:SecretKey", "test-only-signing-key-0123456789-abcdef");
        builder.UseSetting("Jwt:Issuer", "shipwise-tests");
        builder.UseSetting("Jwt:Audience", "shipwise-tests");
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "5");
        builder.UseSetting("IntegrationApi:ApiKeys:0", "test-only-integration-key");
        builder.UseSetting("Registration:Enabled", enabled.ToString());
    }
}
