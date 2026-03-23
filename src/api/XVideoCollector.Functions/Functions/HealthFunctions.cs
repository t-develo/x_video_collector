using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Services;

namespace XVideoCollector.Functions.Functions;

public sealed class HealthFunctions(IHealthCheckService healthCheck)
{
    [Function("HealthCheck")]
    public async Task<IActionResult> CheckAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var result = await healthCheck.CheckAsync(cancellationToken);
        var statusCode = result.Status == HealthStatus.Healthy ? 200 : 503;
        return new ObjectResult(result) { StatusCode = statusCode };
    }
}
