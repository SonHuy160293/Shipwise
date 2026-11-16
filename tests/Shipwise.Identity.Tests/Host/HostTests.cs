using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Shipwise.Identity.Tests.Host;

/// <summary>Host behavior that needs no database.</summary>
public sealed class HostTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
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
        HttpResponseMessage response = await Client(IdentityApiFactory.CreateToken()).GetAsync("/api/v1/probe/ok");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QueryRunsThroughThePipelineWithThePermission()
    {
        HttpResponseMessage response = await Client(IdentityApiFactory.CreateToken("Probe.View")).GetAsync("/api/v1/probe/ok");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("\"data\":\"hello\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotFoundExceptionBecomes404WithItsCode()
    {
        HttpResponseMessage response = await Client(IdentityApiFactory.CreateToken("Probe.View")).GetAsync("/api/v1/probe/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Probe.NotFound", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCommandReturnsFieldErrorsBeforeAnyTransaction()
    {
        HttpResponseMessage response = await Client(IdentityApiFactory.CreateToken("Probe.Create"))
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
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(IdentityDbContext.Schema, context.Model.GetDefaultSchema());
        Assert.Equal("identity", IdentityDbContext.Schema);
    }

    private HttpClient Client(string token)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
