using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Domain.Entities;

public sealed class Tag
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public TagColor Color { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Tag(Guid id, string name, TagColor color, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Color = color;
        CreatedAt = createdAt;
    }

    public static Tag Create(string name, TagColor color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Tag(Guid.NewGuid(), name.Trim(), color, DateTimeOffset.UtcNow);
    }

    public void Update(string name, TagColor color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Color = color;
    }
}
