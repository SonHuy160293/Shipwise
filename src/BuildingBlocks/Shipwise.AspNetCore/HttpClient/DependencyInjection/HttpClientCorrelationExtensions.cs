using Shipwise.AspNetCore.HttpClient.Abstractions;
using Shipwise.AspNetCore.HttpClient.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shipwise.AspNetCore.HttpClient.DependencyInjection;

public static class HttpClientCorrelationExtensions
{
    public static IServiceCollection AddHttpClientCorrelation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Singleton<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>());
        return services;
    }
}
