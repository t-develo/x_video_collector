namespace XVideoCollector.Domain.Entities;

public sealed class Category
{
    public const int MaxNameLength = 100;

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

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));

        return new Category(Guid.NewGuid(), trimmed, sortOrder, timeProvider.GetUtcNow());
    }

    public void Update(string name, int sortOrder, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));

        Name = trimmed;
        SortOrder = sortOrder;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
