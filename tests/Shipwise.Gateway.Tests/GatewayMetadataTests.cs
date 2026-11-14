using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace Shipwise.Gateway.Tests;

public sealed partial class GatewayHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RouteMetadataPreventsCachingForUpstreamAndGatewayErrors(bool fail)
    {
        await using var rig = await Rig.StartAsync(config =>
        {
            config["ReverseProxy"]!["Routes"]!["sample-test"]!["Metadata"] = new JsonObject { ["CacheControl"] = "no-store" };
            if (fail)
            {
                config["ReverseProxy"]!["Clusters"]!["sample"]!["Destinations"]!["sample-api"]!["Address"] = "http://127.0.0.1:1/";
            }
        });
        using var response = await rig.Client.PostAsync(TestRoute, new StringContent("{}"));
        Assert.Equal(fail ? HttpStatusCode.BadGateway : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
