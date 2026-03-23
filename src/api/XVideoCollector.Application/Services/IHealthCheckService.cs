using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Services;

public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}
