# Skill: clean-architecture-dotnet

C# .NET 10 クリーンアーキテクチャのコード生成を標準化するスキル。

## プロジェクト名・名前空間規約

- ソリューション名: `XVideoCollector`
- 名前空間パターン: `XVideoCollector.{Layer}.{Feature}`
- テストプロジェクト名: `XVideoCollector.{Layer}.Tests`

## レイヤー構成と依存ルール

### Domain（`XVideoCollector.Domain`）

- **依存**: なし（外部パッケージ参照禁止）
- **配置物**: エンティティ、値オブジェクト、列挙型、リポジトリインターフェース、ドメインイベント
- **パス**: `src/api/XVideoCollector.Domain/`

```
Domain/
├── Entities/          # エンティティクラス
├── ValueObjects/      # 値オブジェクト（record 推奨）
├── Enums/             # 列挙型
└── Repositories/      # リポジトリインターフェース（I{Entity}Repository）
```

### Application（`XVideoCollector.Application`）

- **依存**: Domain のみ
- **配置物**: ユースケース、DTO、サービスインターフェース
- **パス**: `src/api/XVideoCollector.Application/`

```
Application/
├── UseCases/          # ユースケースクラス（1クラス1ユースケース）
├── DTOs/              # データ転送オブジェクト（record 推奨）
└── Interfaces/        # インフラ向けサービスインターフェース
```

### Infrastructure（`XVideoCollector.Infrastructure`）

- **依存**: Application, Domain
- **配置物**: リポジトリ実装、外部サービス実装、EF Core DbContext、DI 登録
- **パス**: `src/api/XVideoCollector.Infrastructure/`

```
Infrastructure/
├── Persistence/       # DbContext, マイグレーション
├── Repositories/      # リポジトリ具象クラス
├── Services/          # 外部サービス実装
├── Options/           # 設定クラス（IOptions<T>）
└── DependencyInjection.cs  # IServiceCollection 拡張
```

### Functions（`XVideoCollector.Functions`）

- **依存**: Application, Infrastructure（DI 登録のみ）
- **配置物**: Azure Functions エンドポイント、ミドルウェア、Program.cs
- **パス**: `src/api/XVideoCollector.Functions/`

```
Functions/
├── Program.cs         # DI 構成・ミドルウェア登録
├── host.json          # Functions ランタイム設定
├── local.settings.json
├── *Functions.cs      # HTTP トリガー関数
└── Middleware/         # エラーハンドリング等
```

## コード生成パターン

### エンティティ

```csharp
namespace XVideoCollector.Domain.Entities;

public sealed class Video
{
    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    // ... プロパティ（private set）

    // EF Core 用
    #pragma warning disable CS8618
    private Video() { }
    #pragma warning restore CS8618

    // ファクトリメソッドで生成（TimeProvider を使い時刻注入可能にする）
    public static Video Create(/* params */, TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new Video { /* ..., CreatedAt = now, UpdatedAt = now */ };
    }

    // 状態変更メソッドでは必ず UpdatedAt を更新する
    public void UpdateTitle(string title)
    {
        // バリデーション...
        // Title = title;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

### 値オブジェクト

```csharp
namespace XVideoCollector.Domain.ValueObjects;

public record TweetUrl
{
    public string Value { get; }

    public TweetUrl(string url)
    {
        // バリデーション
        if (!IsValid(url))
            throw new ArgumentException($"Invalid tweet URL: {url}");
        Value = Normalize(url);
    }

    private static bool IsValid(string url) { /* ... */ }
    private static string Normalize(string url) { /* ... */ }
}
```

### リポジトリインターフェース

- 検索用パラメータクラス（`VideoSearchQuery` 等）はリポジトリインターフェースとは別ファイルに定義する
- リスト取得はサーバーサイドページング対応メソッドを提供し、全件取得の `GetAllAsync` に依存しない
- Repository 実装は `SaveChangesAsync` を自前で呼ばない（`IUnitOfWork` に委譲）

```csharp
namespace XVideoCollector.Domain.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Video> Items, int TotalCount)> GetPagedAsync(
        int skip, int take, CancellationToken ct = default);
    Task<(IReadOnlyList<Video> Items, int TotalCount)> SearchAsync(
        VideoSearchQuery query, int skip, int take, CancellationToken ct = default);
    Task AddAsync(Video video, CancellationToken ct = default);
    Task UpdateAsync(Video video, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

### ユースケース

UseCase は必ずインターフェースを定義し、DI にはインターフェース経由で登録する（依存性逆転の原則）。

```csharp
// インターフェース定義
namespace XVideoCollector.Application.UseCases;

public interface IRegisterVideoUseCase
{
    Task<VideoDto> ExecuteAsync(
        RegisterVideoRequest request,
        CancellationToken ct = default);
}

// 実装（sealed）
public sealed class RegisterVideoUseCase(
    IVideoRepository videoRepository) : IRegisterVideoUseCase
{
    public async Task<VideoDto> ExecuteAsync(
        RegisterVideoRequest request,
        CancellationToken ct = default)
    {
        // 1. バリデーション
        // 2. ドメインオブジェクト生成
        // 3. リポジトリ呼び出し
        // 4. DTO 変換して返却
    }
}
```

#### Unit of Work パターン

複数リポジトリにまたがる操作は `IUnitOfWork` で1トランザクションにまとめる。

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// UseCase 内での使用例
public sealed class UpdateVideoUseCase(
    IVideoRepository videoRepository,
    IVideoTagRepository videoTagRepository,
    IUnitOfWork unitOfWork) : IUpdateVideoUseCase
{
    public async Task<VideoDto> ExecuteAsync(UpdateVideoRequest request, CancellationToken ct = default)
    {
        // ... 複数のリポジトリ操作 ...
        // 各 Repository は SaveChanges を呼ばず、最後に一括コミット
        await unitOfWork.SaveChangesAsync(ct);
        return dto;
    }
}
```
```

### DTO

```csharp
namespace XVideoCollector.Application.DTOs;

public record VideoDto(
    Guid Id,
    string Title,
    string TweetUrl,
    string Status,
    string? ThumbnailUrl,
    DateTime CreatedAt);

public record RegisterVideoRequest(string TweetUrl);
```

### DI 登録

UseCase はインターフェース経由で登録する。

```csharp
// Application 層
namespace XVideoCollector.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterVideoUseCase, RegisterVideoUseCase>();
        services.AddScoped<IGetVideoUseCase, GetVideoUseCase>();
        // ...
        return services;
    }
}
```

```csharp
// Infrastructure 層
namespace XVideoCollector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("SqlDb")));

        // UnitOfWork（AppDbContext が IUnitOfWork を実装）
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Repositories（internal sealed — SaveChanges を自前で呼ばない）
        services.AddScoped<IVideoRepository, VideoRepository>();

        // Services
        services.AddScoped<IVideoDownloadService, YtDlpDownloadService>();

        // Options
        services.Configure<YtDlpOptions>(configuration.GetSection("YtDlp"));

        return services;
    }
}
```

### Azure Functions エンドポイント

Functions 層は UseCase のインターフェースに依存する（具象クラスに直接依存しない）。
`ReadBodyAsync` や `JsonSerializerOptions` 等の共通処理は `FunctionHelper` に集約し、重複を排除する。

```csharp
namespace XVideoCollector.Functions;

public sealed class VideoFunctions(IRegisterVideoUseCase registerVideo)
{
    [Function("RegisterVideo")]
    public async Task<IActionResult> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "videos")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        var request = await FunctionHelper.ReadBodyAsync<RegisterVideoRequest>(req, cancellationToken);
        if (request is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        var result = await registerVideo.ExecuteAsync(request, cancellationToken);
        return new CreatedAtRouteResult(null, new { id = result.Id }, result);
    }
}
```

**禁止事項:** Azure Functions Consumption Plan では `Task.Run` による fire-and-forget を使用しない。
非同期の長時間処理（動画ダウンロード等）は Queue Trigger に委譲する。
```

## テストパターン（xUnit + Moq）

### ルール

- テストメソッド名: `MethodName_Condition_ExpectedResult` パターン
- AAA（Arrange-Act-Assert）パターンを厳守し、各セクションを空行で区切る
- Mock はインスタンスフィールドまたはコンストラクタで初期化する。**`static readonly Mock` でテスト間の状態を共有しない**
- Moq の `Setup` は暗黙のデフォルト値に依存せず、テストの意図を明示する
- テスト名と内容を一致させる（名前が `Throws` ならアサーションも `ThrowsAsync`）
- テンプレート残骸（空の `UnitTest1.cs` 等）は残さない
- 境界値テスト（0件、1件、ちょうど pageSize 件等）を含める

```csharp
namespace XVideoCollector.Application.Tests.UseCases;

public sealed class RegisterVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _repoMock = new();
    private readonly RegisterVideoUseCase _sut;

    public RegisterVideoUseCaseTests()
    {
        _sut = new RegisterVideoUseCase(_repoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidUrl_ReturnsVideoDto()
    {
        // Arrange
        var request = new RegisterVideoRequest("https://x.com/user/status/123", "Test Video");

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Video", result.Title);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Video>(), default), Times.Once);
    }
}
```

## .csproj テンプレート

### Domain

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

### Application

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\XVideoCollector.Domain\XVideoCollector.Domain.csproj" />
  </ItemGroup>
</Project>
```

### テストプロジェクト

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Moq" Version="4.*" />
  </ItemGroup>
</Project>
```
