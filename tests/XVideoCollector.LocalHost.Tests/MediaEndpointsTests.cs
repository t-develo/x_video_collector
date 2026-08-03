using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using XVideoCollector.Infrastructure.Services;

namespace XVideoCollector.LocalHost.Tests;

/// <summary>
/// 署名付きメディア配信のテスト。
/// Azure Blob の SAS URL に相当する仕組みが正しく機能することを確認する。
/// </summary>
public sealed class MediaEndpointsTests : IClassFixture<LocalHostFactory>, IDisposable
{
    private const string BlobPath = "videos/videos/media-test.mp4";

    private readonly LocalHostFactory _factory;
    private readonly HttpClient _client;
    private readonly byte[] _content;

    public MediaEndpointsTests(LocalHostFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        _content = new byte[4096];
        Random.Shared.NextBytes(_content);
        factory.WriteMediaFile(BlobPath, _content);
    }

    private async Task<string> CreateSignedUrlAsync(string blobPath, TimeSpan expiry)
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<LocalFileStorageService>();

        return await storage.GetSasUrlAsync(blobPath, expiry);
    }

    [Fact]
    public async Task GetMedia_WithValidSignature_ReturnsFullContent()
    {
        var url = await CreateSignedUrlAsync(BlobPath, TimeSpan.FromHours(1));

        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(_content, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetMedia_WithRangeHeader_ReturnsPartialContent()
    {
        var url = await CreateSignedUrlAsync(BlobPath, TimeSpan.FromHours(1));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, 1023);

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        // 動画のシークには Range 対応 (206) が必須
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(1024, body.Length);
        Assert.Equal(_content[..1024], body);
        Assert.Equal(_content.Length, response.Content.Headers.ContentRange?.Length);
    }

    [Fact]
    public async Task GetMedia_WithoutSignature_ReturnsForbidden()
    {
        var response = await _client.GetAsync($"/api/media/{BlobPath}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMedia_WithTamperedSignature_ReturnsForbidden()
    {
        var url = await CreateSignedUrlAsync(BlobPath, TimeSpan.FromHours(1));

        var response = await _client.GetAsync(url + "X");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMedia_WithExpiredSignature_ReturnsForbidden()
    {
        var url = await CreateSignedUrlAsync(BlobPath, TimeSpan.FromSeconds(-1));

        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMedia_ForOtherBlobWithReusedSignature_ReturnsForbidden()
    {
        _factory.WriteMediaFile("videos/videos/other.mp4", _content);

        var url = await CreateSignedUrlAsync(BlobPath, TimeSpan.FromHours(1));
        var query = url[url.IndexOf('?')..];

        var response = await _client.GetAsync($"/api/media/videos/videos/other.mp4{query}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMedia_WithTraversalPath_DoesNotEscapeMediaRoot()
    {
        var response = await _client.GetAsync(
            "/api/media/videos/..%2f..%2f..%2fetc%2fpasswd?exp=99999999999&sig=forged");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMedia_WhenFileMissing_ReturnsNotFound()
    {
        var url = await CreateSignedUrlAsync("videos/videos/does-not-exist.mp4", TimeSpan.FromHours(1));

        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetThumbnail_WhenVideoUnknown_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/thumbnails/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => _client.Dispose();
}
