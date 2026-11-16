using Microsoft.Extensions.DependencyInjection;
using Shipwise.SharedKernel.DependencyInjection;

namespace Shipwise.Identity.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
        => services
            .AddSelfMadeCqrs(AssemblyReference.Assembly)
            .AddApplicationPipelineBehaviors();
}
