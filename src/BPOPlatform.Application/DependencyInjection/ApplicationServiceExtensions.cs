using BPOPlatform.Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BPOPlatform.Application.DependencyInjection;

public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers all Application layer services: MediatR handlers, validators, pipeline behaviours.
    /// Call from Program.cs: <c>builder.Services.AddApplicationServices();</c>
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        return services;
    }
}
