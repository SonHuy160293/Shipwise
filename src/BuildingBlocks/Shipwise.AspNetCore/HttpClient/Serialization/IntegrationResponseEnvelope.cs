namespace Shipwise.AspNetCore.HttpClient.Serialization;

internal sealed record IntegrationResponseEnvelope<T>(bool Success, T? Data, IntegrationResponseError? Error);
