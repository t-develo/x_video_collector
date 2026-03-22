using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using XVideoCollector.Application.Interfaces;

namespace XVideoCollector.Functions.Functions;

public sealed class StatsFunctions(IGetStatsUseCase getStats)
{
    [Function("GetStats")]
    public async Task<IActionResult> GetStatsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stats")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var stats = await getStats.ExecuteAsync(cancellationToken);
        return new OkObjectResult(stats);
    }
}
