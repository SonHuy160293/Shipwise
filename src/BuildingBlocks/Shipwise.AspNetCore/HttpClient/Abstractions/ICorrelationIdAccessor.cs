namespace Shipwise.AspNetCore.HttpClient.Abstractions;

public interface ICorrelationIdAccessor
{
    Guid? GetCorrelationId();
}
