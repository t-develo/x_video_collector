using Microsoft.Extensions.Hosting;
using XVideoCollector.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Build().Run();
