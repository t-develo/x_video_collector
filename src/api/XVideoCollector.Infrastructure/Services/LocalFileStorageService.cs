using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Services;
using XVideoCollector.Infrastructure.Options;

namespace XVideoCollector.Infrastructure.Services;

/// <summary>
/// ローカルファイルシステム上に動画・サムネイルを保存する <see cref="IBlobStorageService"/> 実装。
/// Raspberry Pi 等のスタンドアロン運用向け。
/// Azure Blob 実装 (<c>BlobStorageService</c>) と同じ "{container}/{blobName}" という
/// BlobPath 規約を維持するため、DB に保存される値は両実装で互換性がある。
/// </summary>
public sealed class LocalFileStorageService : IBlobStorageService, ILocalMediaAccessor
{
    private const string ExpiresQueryKey = "exp";
    private const string SignatureQueryKey = "sig";

    private readonly LocalStorageOptions _options;
    private readonly BlobStorageOptions _blobOptions;
    private readonly TimeProvider _timeProvider;
    private readonly string _rootFullPath;
    private readonly byte[] _signingKey;

    public LocalFileStorageService(
        IOptions<LocalStorageOptions> options,
        IOptions<BlobStorageOptions> blobOptions,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _blobOptions = blobOptions.Value;
        _timeProvider = timeProvider;

        if (string.IsNullOrWhiteSpace(_options.RootPath))
            throw new InvalidOperationException("LocalStorage:RootPath is not configured.");

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            throw new InvalidOperationException("LocalStorage:SigningKey is not configured.");

        _rootFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_options.RootPath));
        _signingKey = Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    public long MinimumFreeSpaceBytes => _options.MinimumFreeDiskMB * 1024L * 1024L;

    public Task<string> UploadVideoAsync(
        Stream stream,
        string blobName,
        string contentType = "video/mp4",
        CancellationToken cancellationToken = default)
        => UploadAsync(_blobOptions.VideoContainerName, stream, blobName, cancellationToken);

    public Task<string> UploadThumbnailAsync(
        Stream stream,
        string blobName,
        CancellationToken cancellationToken = default)
        => UploadAsync(_blobOptions.ThumbnailContainerName, stream, blobName, cancellationToken);

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (TryResolveTargetPath(blobPath, out var physicalPath) && File.Exists(physicalPath))
            File.Delete(physicalPath);

        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (!TryResolvePhysicalPath(blobPath, out var physicalPath))
            throw new FileNotFoundException($"Media file not found: {blobPath}");

        Stream stream = new FileStream(
            physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        return Task.FromResult(stream);
    }

    /// <summary>
    /// 署名付きの相対 URL を返す。Azure の SAS URL の代替。
    /// 例: /api/media/videos/videos/{id}.mp4?exp=1234567890&amp;sig=...
    /// </summary>
    public Task<string> GetSasUrlAsync(
        string blobPath,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(expiry).ToUnixTimeSeconds();
        var expires = expiresAt.ToString(CultureInfo.InvariantCulture);
        var signature = ComputeSignature(NormalizeBlobPath(blobPath), expires);

        var encodedPath = string.Join(
            '/',
            NormalizeBlobPath(blobPath).Split('/').Select(Uri.EscapeDataString));

        var pathBase = _options.MediaPathBase.TrimEnd('/');
        var url = $"{pathBase}/{encodedPath}" +
                  $"?{ExpiresQueryKey}={expires}" +
                  $"&{SignatureQueryKey}={Uri.EscapeDataString(signature)}";

        return Task.FromResult(url);
    }

    /// <summary>
    /// ルートディレクトリが存在し書き込み可能であることを確認する。
    /// </summary>
    public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootFullPath);

        var probePath = Path.Combine(_rootFullPath, $".healthcheck_{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllTextAsync(probePath, "ok", cancellationToken);
        }
        finally
        {
            if (File.Exists(probePath))
                File.Delete(probePath);
        }
    }

    public bool TryResolvePhysicalPath(string blobPath, out string physicalPath)
        => TryResolveTargetPath(blobPath, out physicalPath) && File.Exists(physicalPath);

    public bool ValidateSignature(string blobPath, string? expires, string? signature)
    {
        if (string.IsNullOrEmpty(expires) || string.IsNullOrEmpty(signature))
            return false;

        if (!long.TryParse(expires, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAt))
            return false;

        if (expiresAt < _timeProvider.GetUtcNow().ToUnixTimeSeconds())
            return false;

        var expected = ComputeSignature(NormalizeBlobPath(blobPath), expires);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    public long GetAvailableFreeSpaceBytes()
    {
        var drive = DriveInfo.GetDrives()
            .Where(d => d.IsReady && IsUnder(_rootFullPath, d.RootDirectory.FullName))
            .MaxBy(d => d.RootDirectory.FullName.Length);

        return drive?.AvailableFreeSpace ?? 0L;
    }

    public bool HasSufficientFreeSpace() => GetAvailableFreeSpaceBytes() >= MinimumFreeSpaceBytes;

    private async Task<string> UploadAsync(
        string containerName,
        Stream stream,
        string blobName,
        CancellationToken cancellationToken)
    {
        var blobPath = $"{containerName}/{blobName}";

        if (!TryResolveTargetPath(blobPath, out var physicalPath))
            throw new ArgumentException($"Invalid blob path: {blobPath}", nameof(blobName));

        var directory = Path.GetDirectoryName(physicalPath)
            ?? throw new ArgumentException($"Invalid blob path: {blobPath}", nameof(blobName));
        Directory.CreateDirectory(directory);

        await using (var file = new FileStream(
            physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await stream.CopyToAsync(file, cancellationToken);
        }

        return blobPath;
    }

    /// <summary>
    /// BlobPath を物理パスに変換する（ファイルの存在は問わない）。
    /// ルートディレクトリの外を指す場合は false。
    /// </summary>
    private bool TryResolveTargetPath(string blobPath, out string physicalPath)
    {
        physicalPath = string.Empty;

        var normalized = NormalizeBlobPath(blobPath);
        if (normalized.Length == 0)
            return false;

        var segments = normalized.Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".."))
            return false;

        // container 名と blob 名の 2 パート以上が必須（Azure 実装と同じ規約）
        if (segments.Length < 2)
            return false;

        var candidate = Path.GetFullPath(Path.Combine([_rootFullPath, .. segments]));
        if (!IsUnder(candidate, _rootFullPath))
            return false;

        physicalPath = candidate;
        return true;
    }

    private static string NormalizeBlobPath(string blobPath)
        => blobPath.Replace('\\', '/').Trim('/');

    private static bool IsUnder(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root);

        if (path.Equals(normalizedRoot, StringComparison.Ordinal))
            return true;

        // ファイルシステムルート ("/") は TrimEndingDirectorySeparator では区切り文字が残るため、
        // 区切り文字を重ねないようにする
        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return path.StartsWith(prefix, StringComparison.Ordinal);
    }

    private string ComputeSignature(string normalizedBlobPath, string expires)
    {
        var payload = Encoding.UTF8.GetBytes($"{normalizedBlobPath}\n{expires}");
        var hash = HMACSHA256.HashData(_signingKey, payload);

        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
