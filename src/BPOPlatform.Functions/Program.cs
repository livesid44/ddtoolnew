using BPOPlatform.Application.DependencyInjection;
using BPOPlatform.Infrastructure.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(c => c.AddEnvironmentVariables())
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationServices();
        services.AddInfrastructureServices(ctx.Configuration);
    })
    .Build();

host.Run();
