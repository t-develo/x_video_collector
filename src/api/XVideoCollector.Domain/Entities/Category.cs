namespace XVideoCollector.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // EF Core 用
    private Category() { Id = default; Name = string.Empty; }

    private Category(Guid id, string name, int sortOrder, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        SortOrder = sortOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Category Create(string name, int sortOrder, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new Category(Guid.NewGuid(), name.Trim(), sortOrder, timeProvider.GetUtcNow());
    }

    public void Update(string name, int sortOrder, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Name = name.Trim();
        SortOrder = sortOrder;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
