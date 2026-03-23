using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XVideoCollector.Application;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Options;
using XVideoCollector.Infrastructure.Persistence;
using XVideoCollector.Infrastructure.Repositories;
using XVideoCollector.Infrastructure.Services;

namespace XVideoCollector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlDb")
            ?? throw new InvalidOperationException("Connection string 'SqlDb' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            }));

        services.Configure<BlobStorageOptions>(
            configuration.GetSection(BlobStorageOptions.SectionName));

        services.Configure<YtDlpOptions>(
            configuration.GetSection(YtDlpOptions.SectionName));

        services.Configure<QueueStorageOptions>(
            configuration.GetSection(QueueStorageOptions.SectionName));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IVideoTagRepository, VideoTagRepository>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IVideoDownloadService, YtDlpDownloadService>();
        services.AddScoped<IThumbnailService, FfmpegThumbnailService>();
        services.AddScoped<IDownloadQueueService, StorageQueueDownloadQueueService>();
        services.AddScoped<IHealthCheckService, HealthCheckService>();
        services.AddScoped<ITelemetryService, TelemetryService>();

        // TelemetryClient: APPLICATIONINSIGHTS_CONNECTION_STRING が設定されている場合は
        // 接続文字列を使用し、未設定時は空の設定（テレメトリ無効）でフォールバックする
        services.TryAddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var aiConnectionString = cfg["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            TelemetryConfiguration telemetryConfig;
            if (string.IsNullOrEmpty(aiConnectionString))
            {
                telemetryConfig = new TelemetryConfiguration();
            }
            else
            {
                telemetryConfig = TelemetryConfiguration.CreateDefault();
                telemetryConfig.ConnectionString = aiConnectionString;
            }
            return new TelemetryClient(telemetryConfig);
        });

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
