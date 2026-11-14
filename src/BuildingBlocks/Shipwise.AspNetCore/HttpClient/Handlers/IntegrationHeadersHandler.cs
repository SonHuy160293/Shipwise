using Shipwise.AspNetCore.HttpClient.Abstractions;
using Shipwise.AspNetCore.HttpClient.Options;

namespace Shipwise.AspNetCore.HttpClient.Handlers;

public sealed class IntegrationHeadersHandler(
    IntegrationClientOptions options,
    ICorrelationIdAccessor correlationIdAccessor) : DelegatingHandler
{
    public const string CorrelationHeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Guid correlationId = correlationIdAccessor.GetCorrelationId() ?? Guid.NewGuid();
        request.Headers.Remove(CorrelationHeaderName);
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId.ToString("D"));

        request.Headers.Remove(options.ApiKeyHeaderName);
        request.Headers.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
