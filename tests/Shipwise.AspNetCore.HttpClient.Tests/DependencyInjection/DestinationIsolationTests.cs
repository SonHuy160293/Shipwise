using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Shipwise.AspNetCore.HttpClient.Abstractions;
using Shipwise.AspNetCore.HttpClient.DependencyInjection;
using Shipwise.AspNetCore.HttpClient.Options;

using Shipwise.SharedKernel.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Shipwise.AspNetCore.HttpClient.Tests.DependencyInjection;

public sealed class DestinationIsolationTests
{
    [Fact]
    public async Task DestinationsKeepTheirTransportCredentialsAndRetrySettings()
    {
        Guid userId = Guid.NewGuid();
        var values = new Dictionary<string, string?>
        {
            ["Integrations:Primary:BaseUrl"] = "https://primary.test/",
            ["Integrations:Primary:ApiKey"] = "primary-key",
            ["Integrations:Primary:Resilience:Retry:MaxRetryAttempts"] = "1",
            ["Integrations:Secondary:BaseUrl"] = "https://secondary.test/",
            ["Integrations:Secondary:ApiKey"] = "secondary-key",
            ["Integrations:Secondary:Resilience:Retry:MaxRetryAttempts"] = "2",
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        // Match the existing host defaults; registration must replace their resilience pipeline.
        services.ConfigureHttpClientDefaults(builder => builder.AddStandardResilienceHandler());
        var primaryHandler = new CaptureHandler(userId);
        var secondaryHandler = new CaptureHandler(userId);
        services.AddIntegrationHttpClient<IPrimaryIntegrationService, PrimaryIntegrationService>(
            "Primary",
            configuration.GetSection("Integrations:Primary"))
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        services.AddIntegrationHttpClient<ISecondaryClient, SecondaryClient>(
            "Secondary",
            configuration.GetSection("Integrations:Secondary"))
            .ConfigurePrimaryHttpMessageHandler(() => secondaryHandler);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IIntegrationHttpClient<IPrimaryIntegrationService>>());
        Assert.NotNull(provider.GetRequiredService<IIntegrationHttpClient<ISecondaryClient>>());
        IPrimaryIntegrationService primary = provider.GetRequiredService<IPrimaryIntegrationService>();
        ISecondaryClient secondary = provider.GetRequiredService<ISecondaryClient>();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            IntegrationResult<SampleDto> first = await primary.GetUserAsync(userId);
            IntegrationResult<SampleDto> second = await secondary.GetAsync();
            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
        }));

        Assert.Equal(12, primaryHandler.Requests.Count);
        Assert.Equal(12, secondaryHandler.Requests.Count);
        Assert.All(primaryHandler.Requests, request =>
        {
            Assert.Equal(new Uri($"https://primary.test/api/v1/integrations/users/{userId:D}"), request.Uri);
            Assert.Equal("primary-key", request.Key);
        });
        Assert.All(secondaryHandler.Requests, request =>
        {
            Assert.Equal(new Uri("https://secondary.test/users"), request.Uri);
            Assert.Equal("secondary-key", request.Key);
        });
        IOptionsMonitor<IntegrationClientOptions> options =
            provider.GetRequiredService<IOptionsMonitor<IntegrationClientOptions>>();
        Assert.Equal(1, options.Get("Primary").Resilience.Retry.MaxRetryAttempts);
        Assert.Equal(2, options.Get("Secondary").Resilience.Retry.MaxRetryAttempts);
        IOptionsMonitor<HttpStandardResilienceOptions> pipelines =
            provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();
        Assert.Equal(1, pipelines.Get("Primary-standard").Retry.MaxRetryAttempts);
        Assert.Equal(2, pipelines.Get("Secondary-standard").Retry.MaxRetryAttempts);
    }

    public interface IPrimaryIntegrationService
    {
        Task<IntegrationResult<SampleDto>> GetUserAsync(Guid userId);
    }

    public sealed class PrimaryIntegrationService(IIntegrationHttpClient<IPrimaryIntegrationService> client) : IPrimaryIntegrationService
    {
        public Task<IntegrationResult<SampleDto>> GetUserAsync(Guid userId)
            => client.GetAsync<SampleDto>($"api/v1/integrations/users/{userId:D}");
    }

    public interface ISecondaryClient
    {
        Task<IntegrationResult<SampleDto>> GetAsync();
    }

    public sealed class SecondaryClient(IIntegrationHttpClient<ISecondaryClient> client) : ISecondaryClient
    {
        public Task<IntegrationResult<SampleDto>> GetAsync()
            => client.GetAsync<SampleDto>("users");
    }

    private sealed class CaptureHandler(Guid userId) : HttpMessageHandler
    {
        public ConcurrentBag<(Uri? Uri, string Key)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri, request.Headers.GetValues("X-Integration-Api-Key").Single()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    Success = true,
                    Data = new SampleDto(userId, "Ada", null, "active"),
                }),
            });
        }
    }
}
