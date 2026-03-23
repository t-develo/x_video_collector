namespace XVideoCollector.Application.Dtos;

public static class HealthStatus
{
    public const string Healthy = "Healthy";
    public const string Unhealthy = "Unhealthy";
}

public sealed record HealthCheckResult(
    string Status,
    IReadOnlyDictionary<string, HealthCheckEntry> Checks,
    DateTimeOffset Timestamp);

public sealed record HealthCheckEntry(
    string Status,
    string? Message = null,
    long? DurationMs = null);
