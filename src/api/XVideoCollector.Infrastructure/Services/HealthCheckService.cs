using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Services;
using XVideoCollector.Infrastructure.Options;
using XVideoCollector.Infrastructure.Persistence;

namespace XVideoCollector.Infrastructure.Services;

internal sealed class HealthCheckService(
    AppDbContext dbContext,
    IBlobStorageService blobStorageService,
    IOptions<YtDlpOptions> ytDlpOptions,
    TimeProvider timeProvider) : IHealthCheckService
{

    public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, HealthCheckEntry>();

        await RunCheckAsync(checks, "sql", () => CheckSqlAsync(cancellationToken));
        await RunCheckAsync(checks, "blob", () => CheckBlobAsync(cancellationToken));
        RunSync(checks, "ytdlp", CheckYtDlp);
        RunSync(checks, "ffmpeg", CheckFfmpeg);
        RunSync(checks, "ffprobe", CheckFfprobe);

        var overallStatus = checks.Values.All(c => c.Status == HealthStatus.Healthy)
            ? HealthStatus.Healthy
            : HealthStatus.Unhealthy;

        return new HealthCheckResult(overallStatus, checks, timeProvider.GetUtcNow());
    }

    private static async Task RunCheckAsync(
        Dictionary<string, HealthCheckEntry> checks,
        string name,
        Func<Task<HealthCheckEntry>> check)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            checks[name] = await check();
        }
        catch (Exception ex)
        {
            checks[name] = new HealthCheckEntry(HealthStatus.Unhealthy, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private static void RunSync(
        Dictionary<string, HealthCheckEntry> checks,
        string name,
        Func<HealthCheckEntry> check)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            checks[name] = check();
        }
        catch (Exception ex)
        {
            checks[name] = new HealthCheckEntry(HealthStatus.Unhealthy, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private async Task<HealthCheckEntry> CheckSqlAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        sw.Stop();

        return canConnect
            ? new HealthCheckEntry(HealthStatus.Healthy,null, sw.ElapsedMilliseconds)
            : new HealthCheckEntry(HealthStatus.Unhealthy,"Cannot connect to SQL Database.", sw.ElapsedMilliseconds);
    }

    private async Task<HealthCheckEntry> CheckBlobAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await blobStorageService.CheckConnectionAsync(cancellationToken);
        sw.Stop();

        return new HealthCheckEntry(HealthStatus.Healthy,null, sw.ElapsedMilliseconds);
    }

    private HealthCheckEntry CheckYtDlp()
    {
        var sw = Stopwatch.StartNew();
        var path = ytDlpOptions.Value.ExecutablePath;
        var exists = File.Exists(path);
        sw.Stop();

        return exists
            ? new HealthCheckEntry(HealthStatus.Healthy,path, sw.ElapsedMilliseconds)
            : new HealthCheckEntry(HealthStatus.Unhealthy,$"Binary not found: {path}", sw.ElapsedMilliseconds);
    }

    private HealthCheckEntry CheckFfmpeg()
    {
        var sw = Stopwatch.StartNew();
        var path = ytDlpOptions.Value.FfmpegPath;
        var exists = File.Exists(path);
        sw.Stop();

        return exists
            ? new HealthCheckEntry(HealthStatus.Healthy,path, sw.ElapsedMilliseconds)
            : new HealthCheckEntry(HealthStatus.Unhealthy,$"Binary not found: {path}", sw.ElapsedMilliseconds);
    }

    private HealthCheckEntry CheckFfprobe()
    {
        var sw = Stopwatch.StartNew();
        var path = ytDlpOptions.Value.FfprobePath;
        var exists = File.Exists(path);
        sw.Stop();

        return exists
            ? new HealthCheckEntry(HealthStatus.Healthy,path, sw.ElapsedMilliseconds)
            : new HealthCheckEntry(HealthStatus.Unhealthy,$"Binary not found: {path}", sw.ElapsedMilliseconds);
    }
}
