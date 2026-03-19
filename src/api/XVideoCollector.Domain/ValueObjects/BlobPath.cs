namespace XVideoCollector.Domain.ValueObjects;

public sealed record BlobPath
{
    public string Value { get; }

    private BlobPath(string value)
    {
        Value = value;
    }

    public static BlobPath Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Trim().TrimStart('/');
        if (normalized.Length == 0)
            throw new ArgumentException("Blob path must not be empty.", nameof(path));

        return new BlobPath(normalized);
    }

    public override string ToString() => Value;
}
