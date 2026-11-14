using Shipwise.AspNetCore.HttpClient.Options.Resilience;

namespace Shipwise.AspNetCore.HttpClient.Options;

public sealed class IntegrationClientOptions
{
    public Uri? BaseUrl { get; init; }

    public string ApiKeyHeaderName { get; init; } = "X-Integration-Api-Key";

    public string ApiKey { get; init; } = string.Empty;

    public IntegrationResilienceOptions Resilience { get; init; } = new();
}
