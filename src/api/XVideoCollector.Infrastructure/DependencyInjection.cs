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
    /// <summary>"Storage:Provider" にこの値を指定するとローカルファイルシステムを使用する。</summary>
    public const string LocalStorageProvider = "Local";

    /// <summary>"Queue:Provider" にこの値を指定するとプロセス内キューを使用する。</summary>
    public const string InProcessQueueProvider = "InProcess";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlDb")
            ?? throw new InvalidOperationException("Connection string 'SqlDb' is not configured.");

        // SQLite はローカル開発用 (接続文字列が "Data Source=" で始まる場合)
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
            services.AddHostedService<SqliteEnsureCreatedService>();
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                }));
        }

        services.Configure<BlobStorageOptions>(
            configuration.GetSection(BlobStorageOptions.SectionName));

        services.Configure<YtDlpOptions>(
            configuration.GetSection(YtDlpOptions.SectionName));

        services.Configure<QueueStorageOptions>(
            configuration.GetSection(QueueStorageOptions.SectionName));

        services.Configure<LocalStorageOptions>(
            configuration.GetSection(LocalStorageOptions.SectionName));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IVideoTagRepository, VideoTagRepository>();
        services.AddScoped<IVideoDownloadService, YtDlpDownloadService>();
        services.AddScoped<IThumbnailService, FfmpegThumbnailService>();
        services.AddScoped<IHealthCheckService, HealthCheckService>();

        AddStorage(services, configuration);
        AddDownloadQueue(services, configuration);

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

    /// <summary>
    /// メディア保存先の実装を選択する。
    /// 既定は Azure Blob Storage で、"Storage:Provider" に "Local" を指定した場合のみ
    /// ローカルファイルシステム実装を使用する（Raspberry Pi 等のスタンドアロン運用）。
    /// </summary>
    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        if (IsProvider(configuration, "Storage:Provider", LocalStorageProvider))
        {
            services.AddSingleton<LocalFileStorageService>();
            services.AddSingleton<ILocalMediaAccessor>(sp => sp.GetRequiredService<LocalFileStorageService>());
            services.AddScoped<IBlobStorageService>(sp => sp.GetRequiredService<LocalFileStorageService>());
        }
        else
        {
            services.AddScoped<IBlobStorageService, BlobStorageService>();
        }
    }

    /// <summary>
    /// ダウンロードキューの実装を選択する。
    /// 既定は Azure Storage Queue で、"Queue:Provider" に "InProcess" を指定した場合のみ
    /// プロセス内チャネル実装を使用する。
    /// </summary>
    private static void AddDownloadQueue(IServiceCollection services, IConfiguration configuration)
    {
        if (IsProvider(configuration, "Queue:Provider", InProcessQueueProvider))
        {
            services.AddSingleton<DownloadQueueChannel>();
            services.AddScoped<IDownloadQueueService, InProcessDownloadQueueService>();
        }
        else
        {
            services.AddScoped<IDownloadQueueService, StorageQueueDownloadQueueService>();
        }
    }

    private static bool IsProvider(IConfiguration configuration, string key, string expected)
        => string.Equals(configuration[key], expected, StringComparison.OrdinalIgnoreCase);
}
