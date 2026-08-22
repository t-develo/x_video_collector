using Microsoft.Extensions.FileProviders;
using XVideoCollector.Application;
using XVideoCollector.Infrastructure;
using XVideoCollector.LocalHost.Endpoints;
using XVideoCollector.LocalHost.Helpers;
using XVideoCollector.LocalHost.Middleware;
using XVideoCollector.LocalHost.Workers;

var builder = WebApplication.CreateBuilder(args);

// systemd の Type=notify と journald 形式のログに対応する
builder.Host.UseSystemd();

var frontendRoot = FrontendRootResolver.Resolve(builder.Environment.ContentRootPath, builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<DownloadWorkerOptions>(
    builder.Configuration.GetSection(DownloadWorkerOptions.SectionName));
builder.Services.AddHostedService<DownloadWorker>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    foreach (var converter in RequestHelper.JsonOptions.Converters)
        options.SerializerOptions.Converters.Add(converter);

    options.SerializerOptions.PropertyNamingPolicy = RequestHelper.JsonOptions.PropertyNamingPolicy;
});

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<ClientPrincipalMiddleware>();

// フロントエンドの配信元は WebRoot ではなくファイルプロバイダーで明示する
// （発行時は wwwroot、リポジトリから直接起動した場合は src/frontend を指す）
var frontendFileProvider = frontendRoot is not null
    ? new PhysicalFileProvider(frontendRoot)
    : null;

if (frontendFileProvider is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendFileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendFileProvider });
}

app.MapVideoEndpoints();
app.MapTagEndpoints();
app.MapCategoryEndpoints();
app.MapMediaEndpoints();
app.MapSystemEndpoints();

// SPA フォールバック（/api と /.auth は上のルートが優先される）
if (frontendFileProvider is not null)
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = frontendFileProvider });

app.Logger.LogInformation(
    "XVideoCollector LocalHost を起動します (フロントエンド={FrontendRoot})",
    frontendRoot ?? "(未配置)");

try
{
    app.Run();
}
catch (Exception ex)
{
    // ポート競合はラズパイ運用で最も起きやすい起動失敗。
    // 未処理例外のまま abort させるとスタックトレースだけが journal に残り原因が読めない。
    var reason = StartupFailure.DescribeAddressInUse(ex, builder.Configuration["ASPNETCORE_URLS"]);
    if (reason is null)
        throw;

    app.Logger.LogCritical(ex, "{Reason}", reason);
    return 1;
}

return 0;

/// <summary>
/// フロントエンド SPA の配信元ディレクトリを解決する。
/// 発行時は wwwroot、リポジトリから直接起動した場合は src/frontend を使用する。
/// </summary>
internal static class FrontendRootResolver
{
    internal static string? Resolve(string contentRootPath, IConfiguration configuration)
    {
        var candidates = new[]
        {
            configuration["Frontend:RootPath"],
            Path.Combine(contentRootPath, "wwwroot"),
            // リポジトリ内から dotnet run した場合 (src/api/XVideoCollector.LocalHost → src/frontend)
            Path.Combine(contentRootPath, "..", "..", "frontend"),
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "index.html")))
                return fullPath;
        }

        return null;
    }
}

/// <summary>統合テストからエントリポイントを参照するためのマーカー。</summary>
public partial class Program;
