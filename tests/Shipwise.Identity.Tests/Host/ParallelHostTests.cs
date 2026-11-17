using System.Net;
using Xunit;

namespace Shipwise.Identity.Tests.Host;

/// <summary>
/// Test classes run in parallel, so several API hosts start in one process at the same time.
/// The host's logging setup must allow that.
/// </summary>
public sealed class ParallelHostTests
{
    [Fact]
    public async Task SeveralHostsCanStartAtTheSameTime()
    {
        const int hosts = 8;
        var factories = Enumerable.Range(0, hosts).Select(_ => new IdentityApiFactory()).ToArray();
        try
        {
            HttpResponseMessage[] responses = await Task.WhenAll(factories.Select(factory => Task.Run(
                () => factory.CreateClient().GetAsync("/health/live"))));

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
        finally
        {
            foreach (IdentityApiFactory factory in factories)
            {
                await factory.DisposeAsync();
            }
        }
    }
}
