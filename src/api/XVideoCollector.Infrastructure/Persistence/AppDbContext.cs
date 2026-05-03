using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Infrastructure.Persistence.Configurations;

namespace XVideoCollector.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<VideoTag> VideoTags => Set<VideoTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new VideoConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new VideoTagConfiguration());

        // SQLite は DateTimeOffset を直接サポートしないため long (UTC ticks) に変換する
        if (Database.IsSqlite())
        {
            var converter = new ValueConverter<DateTimeOffset, long>(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            var nullableConverter = new ValueConverter<DateTimeOffset?, long?>(
                v => v == null ? null : v.Value.UtcTicks,
                v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                        property.SetValueConverter(converter);
                    else if (property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(nullableConverter);
                }
            }
        }
    }
}
