using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Domain.Entities;

public sealed class Tag
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public TagColor Color { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // EF Core 用
    private Tag() { Id = default; Name = string.Empty; }

    private Tag(Guid id, string name, TagColor color, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Color = color;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Tag Create(string name, TagColor color, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));

        return new Tag(Guid.NewGuid(), trimmed, color, timeProvider.GetUtcNow());
    }

    public void Update(string name, TagColor color, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));

        Name = trimmed;
        Color = color;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
