namespace XVideoCollector.Domain.ValueObjects;

public sealed record VideoTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    private VideoTitle(string value)
    {
        Value = value;
    }

    public static VideoTitle Create(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var trimmed = title.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Video title must not exceed {MaxLength} characters.", nameof(title));

        return new VideoTitle(trimmed);
    }

    public override string ToString() => Value;
}
