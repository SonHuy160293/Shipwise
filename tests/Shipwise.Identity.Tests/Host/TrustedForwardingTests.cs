using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shipwise.AspNetCore.DependencyInjection;
using Xunit;

namespace Shipwise.Identity.Tests.Host;

public sealed class TrustedForwardingTests
{
    [Theory]
    [InlineData("192.0.2.0/24", null, "192.0.2.42", true)]
    [InlineData("192.0.2.0/24", null, "192.0.3.42", false)]
    [InlineData("192.0.2.0/24", null, "127.0.0.1", false)]
    [InlineData("2001:db8:1::/64", null, "2001:db8:1::42", true)]
    [InlineData("192.0.2.0/24", "198.51.100.1", "198.51.100.1", true)]
    [InlineData("192.0.2.0/24", "198.51.100.1", "192.0.2.2", true)]
    [InlineData(null, "198.51.100.1", "127.0.0.1", false)]
    [InlineData(null, null, "127.0.0.1", true)]
    [InlineData(null, null, "::1", true)]
    [InlineData(null, null, "192.0.2.42", false)]
    public async Task DownstreamSeesForwardedClientOnlyFromTrustedPeer(
        string? network, string? proxy, string peer, bool trusted)
    {
        var settings = new Dictionary<string, string?>();
        if (network is not null)
        {
            settings.Add("ForwardedHeaders:KnownNetworks:0", network);
        }

        if (proxy is not null)
        {
            settings.Add("ForwardedHeaders:KnownProxies:0", proxy);
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var services = new ServiceCollection().AddTrustedForwarding(configuration).BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        const string client = "203.0.113.7";
        context.Request.Headers["X-Forwarded-For"] = client;
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, options);

        await middleware.Invoke(context);
        Assert.Equal(IPAddress.Parse(trusted ? client : peer), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public void EmptyListsInJsonUseLoopbackOnly()
    {
        using var json = new MemoryStream("""{"ForwardedHeaders":{"KnownProxies":[],"KnownNetworks":[]}}"""u8.ToArray());
        var configuration = new ConfigurationBuilder().AddJsonStream(json).Build();
        using var services = new ServiceCollection().AddTrustedForwarding(configuration).BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal([IPAddress.Loopback, IPAddress.IPv6Loopback], options.KnownProxies);
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "invalid/24")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "192.0.2.0")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "192.0.2.0/33")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10/8")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "2001:db8::/129")]
    [InlineData("ForwardedHeaders:KnownNetworks", "malformed")]
    public void InvalidNetworkFailsDuringRegistration(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [key] = value }).Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTrustedForwarding(configuration));
    }
}
