using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XVideoCollector.Application;
using XVideoCollector.Functions.Helpers;
using XVideoCollector.Functions.Middleware;
using XVideoCollector.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerApp =>
    {
        workerApp.UseMiddleware<ExceptionMiddleware>();
        workerApp.UseMiddleware<AuthMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);

        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            foreach (var converter in FunctionHelper.JsonOptions.Converters)
                options.SerializerOptions.Converters.Add(converter);
            options.SerializerOptions.PropertyNamingPolicy = FunctionHelper.JsonOptions.PropertyNamingPolicy;
        });
    })
    .Build();

await host.RunAsync();
