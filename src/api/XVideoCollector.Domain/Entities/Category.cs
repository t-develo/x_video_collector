namespace XVideoCollector.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Category(Guid id, string name, int sortOrder, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        SortOrder = sortOrder;
        CreatedAt = createdAt;
    }

    public static Category Create(string name, int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Category(Guid.NewGuid(), name.Trim(), sortOrder, DateTimeOffset.UtcNow);
    }

    public void Update(string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        SortOrder = sortOrder;
    }
}
