using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XVideoCollector.Infrastructure.Persistence;

// SQLite ローカル開発用: アプリ起動時にスキーマを自動作成する
internal sealed class SqliteEnsureCreatedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SqliteEnsureCreatedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        logger.LogInformation("SQLite database schema ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
