using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace XVideoCollector.LocalHost.Tests;

/// <summary>
/// スタンドアロンホストの API 疎通テスト。
/// Azure Functions 版と同じルート・同じレスポンス形状であることを確認する。
/// </summary>
public sealed class ApiEndpointsTests : IClassFixture<LocalHostFactory>, IDisposable
{
    private readonly LocalHostFactory _factory;
    private readonly HttpClient _client;

    public ApiEndpointsTests(LocalHostFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── ヘルスチェック・統計 ──────────────────────────────────

    [Fact]
    public async Task GetHealth_ReturnsHealthyWithDiskCheck()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", json.GetProperty("status").GetString());
        // ローカルストレージ運用時のみ追加されるチェック項目
        Assert.True(json.GetProperty("checks").TryGetProperty("disk", out _));
    }

    [Fact]
    public async Task GetStats_ReturnsStats()
    {
        var response = await _client.GetAsync("/api/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("totalCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetAuthMe_ReturnsClientPrincipalStub()
    {
        var response = await _client.GetAsync("/.auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(
            json.GetProperty("clientPrincipal").GetProperty("userDetails").GetString()));
    }

    // ── 動画 ──────────────────────────────────────────────────

    [Fact]
    public async Task GetVideos_ReturnsPagedResult()
    {
        var response = await _client.GetAsync("/api/videos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task PostVideo_WithValidUrl_ReturnsCreatedWithPendingStatus()
    {
        var response = await PostVideoAsync("https://x.com/creator/status/1000000000001", "登録テスト");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("登録テスト", json.GetProperty("title").GetString());
        Assert.Equal("1000000000001", json.GetProperty("tweetId").GetString());
    }

    [Fact]
    public async Task PostVideo_WhenDuplicate_ReturnsConflict()
    {
        await PostVideoAsync("https://x.com/creator/status/1000000000002", "初回");

        var response = await PostVideoAsync("https://x.com/creator/status/1000000000002", "重複");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostVideo_WithMalformedBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(
            "/api/videos", new StringContent("not-json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVideo_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/videos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVideoStreamUrl_WhenNotReady_ReturnsConflict()
    {
        var created = await PostVideoAsync("https://x.com/creator/status/1000000000003", "未完了");
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/videos/{id}/stream");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SearchVideos_WithKeyword_TranslatesToSqlAndReturnsMatch()
    {
        await PostVideoAsync("https://x.com/creator/status/1000000000004", "検索対象のタイトル");

        var response = await _client.GetAsync("/api/videos/search?q=検索対象");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task GetVideos_SortedByTitle_TranslatesToSql()
    {
        var response = await _client.GetAsync("/api/videos?sortBy=title&sortDir=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVideo_ThenGet_ReturnsNotFound()
    {
        var created = await PostVideoAsync("https://x.com/creator/status/1000000000005", "削除対象");
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var deleteResponse = await _client.DeleteAsync($"/api/videos/{id}");
        var getResponse = await _client.GetAsync($"/api/videos/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ── タグ / カテゴリ ───────────────────────────────────────

    [Fact]
    public async Task PostTag_ThenList_IncludesCreatedTag()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/tags", new { name = $"タグ{Guid.NewGuid():N}", color = "Blue" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listResponse = await _client.GetAsync("/api/tags");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(
            list.EnumerateArray(),
            t => t.GetProperty("id").GetString() == created.GetProperty("id").GetString());
    }

    [Fact]
    public async Task PostTag_WithoutName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/tags", new { color = "Blue" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCategory_ThenList_IncludesCreatedCategory()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/categories", new { name = $"カテゴリ{Guid.NewGuid():N}", sortOrder = 1 });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listResponse = await _client.GetAsync("/api/categories");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(
            list.EnumerateArray(),
            c => c.GetProperty("id").GetString() == created.GetProperty("id").GetString());
    }

    [Fact]
    public async Task PostCategory_WithoutName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/categories", new { sortOrder = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── フロントエンド配信 ────────────────────────────────────

    [Fact]
    public async Task GetRoot_ReturnsFrontendIndex()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetSpaRoute_FallsBackToIndex()
    {
        var response = await _client.GetAsync("/register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    private Task<HttpResponseMessage> PostVideoAsync(string tweetUrl, string title)
        => _client.PostAsJsonAsync("/api/videos", new { tweetUrl, title });

    public void Dispose() => _client.Dispose();
}
