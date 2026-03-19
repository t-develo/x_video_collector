using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;
using XVideoCollector.Application;
using XVideoCollector.Functions.Middleware;
using XVideoCollector.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerApp =>
    {
        workerApp.UseMiddleware<ExceptionMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);

        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
    })
    .Build();

await host.RunAsync();
