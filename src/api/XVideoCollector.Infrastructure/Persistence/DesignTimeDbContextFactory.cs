using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace XVideoCollector.Infrastructure.Persistence;

// EF Core CLI ツール用デザインタイムファクトリ
// dotnet ef migrations add / dotnet ef database update で使用される
// 本番環境では使用されない
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=XVideoCollector;Trusted_Connection=True;",
            sqlOptions => sqlOptions.EnableRetryOnFailure());

        return new AppDbContext(optionsBuilder.Options);
    }
}
