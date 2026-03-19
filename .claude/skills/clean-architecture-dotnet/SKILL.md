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

public class Video
{
    public Guid Id { get; private set; }
    // ... プロパティ（private set）

    // ファクトリメソッドまたはコンストラクタで生成
    public static Video Create(/* params */)
    {
        return new Video { /* ... */ };
    }

    // ドメインロジックはエンティティ内メソッドとして定義
    public void UpdateTitle(string title) { /* ... */ }
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

```csharp
namespace XVideoCollector.Domain.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Video>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Video video, CancellationToken ct = default);
    Task UpdateAsync(Video video, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

### ユースケース

```csharp
namespace XVideoCollector.Application.UseCases;

public class RegisterVideoUseCase(
    IVideoRepository videoRepository,
    IVideoDownloadService downloadService)
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

```csharp
namespace XVideoCollector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
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

```csharp
namespace XVideoCollector.Functions;

public class VideoFunctions(RegisterVideoUseCase registerVideo)
{
    [Function("RegisterVideo")]
    public async Task<HttpResponseData> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "videos")]
        HttpRequestData req)
    {
        var request = await req.ReadFromJsonAsync<RegisterVideoRequest>();
        var result = await registerVideo.ExecuteAsync(request!);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(result);
        return response;
    }
}
```

## テストパターン（xUnit + Moq）

```csharp
namespace XVideoCollector.Application.Tests.UseCases;

public class RegisterVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _repoMock = new();
    private readonly Mock<IVideoDownloadService> _dlMock = new();
    private readonly RegisterVideoUseCase _sut;

    public RegisterVideoUseCaseTests()
    {
        _sut = new RegisterVideoUseCase(_repoMock.Object, _dlMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidUrl_ReturnsVideoDto()
    {
        // Arrange
        var request = new RegisterVideoRequest("https://x.com/user/status/123");

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
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
