using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateApplicationBuilder(args);
host.Services.AddSingleton<string>("placeholder");
host.Build().Run();
