using System.Text;
using Microsoft.Extensions.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using XVideoCollector.Infrastructure.Options;
using XVideoCollector.Infrastructure.Services;

namespace XVideoCollector.Infrastructure.Tests.Services;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private const string SigningKey = "test-signing-key-0123456789";

    private readonly string _rootPath;
    private readonly FakeTimeProvider _timeProvider;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"xvc_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);

        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        _sut = CreateService(_rootPath, _timeProvider);
    }

    private static LocalFileStorageService CreateService(
        string rootPath,
        TimeProvider timeProvider,
        long minimumFreeDiskMB = 1)
    {
        var localOptions = MsOptions.Create(new LocalStorageOptions
        {
            RootPath = rootPath,
            SigningKey = SigningKey,
            MediaPathBase = "/api/media",
            MinimumFreeDiskMB = minimumFreeDiskMB,
        });

        var blobOptions = MsOptions.Create(new BlobStorageOptions
        {
            VideoContainerName = "videos",
            ThumbnailContainerName = "thumbnails",
        });

        return new LocalFileStorageService(localOptions, blobOptions, timeProvider);
    }

    private static MemoryStream StreamOf(string content) => new(Encoding.UTF8.GetBytes(content));

    // ── アップロード / 読み取り / 削除 ────────────────────────

    [Fact]
    public async Task UploadVideoAsync_ReturnsAzureCompatibleBlobPath()
    {
        var blobPath = await _sut.UploadVideoAsync(StreamOf("video-body"), "videos/abc.mp4");

        Assert.Equal("videos/videos/abc.mp4", blobPath);
    }

    [Fact]
    public async Task UploadThumbnailAsync_ReturnsAzureCompatibleBlobPath()
    {
        var blobPath = await _sut.UploadThumbnailAsync(StreamOf("jpeg-body"), "thumbnails/abc.jpg");

        Assert.Equal("thumbnails/thumbnails/abc.jpg", blobPath);
    }

    [Fact]
    public async Task UploadVideoAsync_WritesFileUnderContainerDirectory()
    {
        await _sut.UploadVideoAsync(StreamOf("video-body"), "videos/abc.mp4");

        var expectedPath = Path.Combine(_rootPath, "videos", "videos", "abc.mp4");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("video-body", await File.ReadAllTextAsync(expectedPath));
    }

    [Fact]
    public async Task OpenReadAsync_AfterUpload_ReturnsSameContent()
    {
        var blobPath = await _sut.UploadVideoAsync(StreamOf("round-trip"), "videos/rt.mp4");

        await using var stream = await _sut.OpenReadAsync(blobPath);
        using var reader = new StreamReader(stream);

        Assert.Equal("round-trip", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenReadAsync_WhenMissing_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _sut.OpenReadAsync("videos/videos/missing.mp4"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var blobPath = await _sut.UploadVideoAsync(StreamOf("to-delete"), "videos/del.mp4");

        await _sut.DeleteAsync(blobPath);

        Assert.False(_sut.TryResolvePhysicalPath(blobPath, out _));
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_DoesNotThrow()
    {
        await _sut.DeleteAsync("videos/videos/never-existed.mp4");
    }

    [Fact]
    public async Task UploadVideoAsync_OverwritesExistingFile()
    {
        await _sut.UploadVideoAsync(StreamOf("first"), "videos/same.mp4");
        var blobPath = await _sut.UploadVideoAsync(StreamOf("second"), "videos/same.mp4");

        await using var stream = await _sut.OpenReadAsync(blobPath);
        using var reader = new StreamReader(stream);

        Assert.Equal("second", await reader.ReadToEndAsync());
    }

    // ── パス解決とディレクトリトラバーサル ────────────────────

    [Theory]
    [InlineData("videos/../../etc/passwd")]
    [InlineData("../etc/passwd")]
    [InlineData("videos/../../../secret.txt")]
    [InlineData("videos")]              // container のみでファイル名が無い
    [InlineData("")]
    [InlineData("/")]
    public void TryResolvePhysicalPath_WithInvalidPath_ReturnsFalse(string blobPath)
    {
        Assert.False(_sut.TryResolvePhysicalPath(blobPath, out _));
    }

    [Fact]
    public async Task TryResolvePhysicalPath_WithBackslashSeparator_ResolvesToSameFile()
    {
        await _sut.UploadVideoAsync(StreamOf("body"), "videos/win.mp4");

        Assert.True(_sut.TryResolvePhysicalPath(@"videos\videos\win.mp4", out var physicalPath));
        Assert.Equal(Path.Combine(_rootPath, "videos", "videos", "win.mp4"), physicalPath);
    }

    // ── 署名付き URL ──────────────────────────────────────────

    [Fact]
    public async Task GetSasUrlAsync_ReturnsUrlWithExpiryAndSignature()
    {
        var url = await _sut.GetSasUrlAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        Assert.StartsWith("/api/media/videos/videos/abc.mp4?", url);
        Assert.Contains($"exp={_timeProvider.GetUtcNow().AddHours(1).ToUnixTimeSeconds()}", url);
        Assert.Contains("&sig=", url);
    }

    [Fact]
    public async Task ValidateSignature_WithGeneratedSignature_ReturnsTrue()
    {
        var (expires, signature) = await GenerateSignatureAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        Assert.True(_sut.ValidateSignature("videos/videos/abc.mp4", expires, signature));
    }

    [Fact]
    public async Task ValidateSignature_WhenExpired_ReturnsFalse()
    {
        var (expires, signature) = await GenerateSignatureAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        _timeProvider.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        Assert.False(_sut.ValidateSignature("videos/videos/abc.mp4", expires, signature));
    }

    [Fact]
    public async Task ValidateSignature_AtExactExpiry_ReturnsTrue()
    {
        var (expires, signature) = await GenerateSignatureAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        _timeProvider.Advance(TimeSpan.FromHours(1));

        Assert.True(_sut.ValidateSignature("videos/videos/abc.mp4", expires, signature));
    }

    [Fact]
    public async Task ValidateSignature_WithTamperedSignature_ReturnsFalse()
    {
        var (expires, signature) = await GenerateSignatureAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        Assert.False(_sut.ValidateSignature("videos/videos/abc.mp4", expires, signature + "X"));
    }

    [Fact]
    public async Task ValidateSignature_ForDifferentBlobPath_ReturnsFalse()
    {
        var (expires, signature) = await GenerateSignatureAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        Assert.False(_sut.ValidateSignature("videos/videos/other.mp4", expires, signature));
    }

    [Fact]
    public async Task ValidateSignature_WithDifferentSigningKey_ReturnsFalse()
    {
        var (expires, signature) = await GenerateSignatureAsync("videos/videos/abc.mp4", TimeSpan.FromHours(1));

        var otherOptions = MsOptions.Create(new LocalStorageOptions
        {
            RootPath = _rootPath,
            SigningKey = "a-completely-different-key",
        });
        var otherService = new LocalFileStorageService(
            otherOptions, MsOptions.Create(new BlobStorageOptions()), _timeProvider);

        Assert.False(otherService.ValidateSignature("videos/videos/abc.mp4", expires, signature));
    }

    [Theory]
    [InlineData(null, "sig")]
    [InlineData("123", null)]
    [InlineData("", "sig")]
    [InlineData("not-a-number", "sig")]
    public void ValidateSignature_WithMissingOrMalformedQuery_ReturnsFalse(string? expires, string? signature)
    {
        Assert.False(_sut.ValidateSignature("videos/videos/abc.mp4", expires, signature));
    }

    private async Task<(string Expires, string Signature)> GenerateSignatureAsync(string blobPath, TimeSpan expiry)
    {
        var url = await _sut.GetSasUrlAsync(blobPath, expiry);
        var query = url[(url.IndexOf('?') + 1)..].Split('&');

        var expires = query.First(p => p.StartsWith("exp=")).Split('=')[1];
        var signature = Uri.UnescapeDataString(query.First(p => p.StartsWith("sig=")).Split('=')[1]);

        return (expires, signature);
    }

    // ── 設定検証 ──────────────────────────────────────────────

    [Fact]
    public void Constructor_WithoutRootPath_Throws()
    {
        var options = MsOptions.Create(new LocalStorageOptions { SigningKey = SigningKey });

        Assert.Throws<InvalidOperationException>(
            () => new LocalFileStorageService(options, MsOptions.Create(new BlobStorageOptions()), _timeProvider));
    }

    [Fact]
    public void Constructor_WithoutSigningKey_Throws()
    {
        var options = MsOptions.Create(new LocalStorageOptions { RootPath = _rootPath });

        Assert.Throws<InvalidOperationException>(
            () => new LocalFileStorageService(options, MsOptions.Create(new BlobStorageOptions()), _timeProvider));
    }

    // ── ヘルスチェック / 空き容量 ────────────────────────────

    [Fact]
    public async Task CheckConnectionAsync_WhenRootIsWritable_Succeeds()
    {
        await _sut.CheckConnectionAsync();

        Assert.Empty(Directory.GetFiles(_rootPath, ".healthcheck_*"));
    }

    [Fact]
    public async Task CheckConnectionAsync_WhenRootMissing_CreatesIt()
    {
        var missingRoot = Path.Combine(_rootPath, "not-yet-created");
        var service = CreateService(missingRoot, _timeProvider);

        await service.CheckConnectionAsync();

        Assert.True(Directory.Exists(missingRoot));
    }

    [Fact]
    public void GetAvailableFreeSpaceBytes_ReturnsPositiveValue()
    {
        Assert.True(_sut.GetAvailableFreeSpaceBytes() > 0);
    }

    [Fact]
    public void HasSufficientFreeSpace_WithSmallMinimum_ReturnsTrue()
    {
        Assert.True(_sut.HasSufficientFreeSpace());
    }

    [Fact]
    public void HasSufficientFreeSpace_WithUnreachableMinimum_ReturnsFalse()
    {
        var service = CreateService(_rootPath, _timeProvider, minimumFreeDiskMB: long.MaxValue / (1024 * 1024));

        Assert.False(service.HasSufficientFreeSpace());
    }

    [Fact]
    public void MinimumFreeSpaceBytes_ConvertsMegabytesToBytes()
    {
        var service = CreateService(_rootPath, _timeProvider, minimumFreeDiskMB: 2048);

        Assert.Equal(2048L * 1024 * 1024, service.MinimumFreeSpaceBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}

/// <summary>署名の有効期限を検証するためのテスト用時刻プロバイダー。</summary>
internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
