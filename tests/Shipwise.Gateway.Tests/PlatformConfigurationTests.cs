using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shipwise.Gateway;
using Xunit;

namespace Shipwise.Gateway.Tests;

public sealed class PlatformConfigurationTests
{
    [Fact]
    public async Task EmptyDevelopmentGatewayIsLiveButNotReadyAndPublishesNoModuleRoutes()
    {
        var builder = CreateBuilder("Development");
        await using var app = GatewayApplication.Build(builder);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        foreach (string path in new[] { "/identity/api/v1/auth/login", "/notification/api/v1/webhooks/email/resend", "/projects/api/v1/engagements" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(path, new StringContent("{}"))).StatusCode);
        }
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task EmptyRoutingRequiresExplicitDevelopmentOptIn(string environment, bool allowEmpty)
    {
        var builder = CreateBuilder(environment);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:AllowEmptyConfiguration"] = allowEmpty.ToString() });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var app = GatewayApplication.Build(builder);
            await app.StartAsync();
        });
    }

    private static WebApplicationBuilder CreateBuilder(string environment)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/Shipwise.Gateway"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = environment, ApplicationName = typeof(GatewayApplication).Assembly.FullName });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
            ["AllowedHosts"] = "localhost;127.0.0.1",
            ["Cors:AllowedOrigins:0"] = "https://frontend.test",
        });
        return builder;
    }
}
