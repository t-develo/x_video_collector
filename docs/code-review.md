# 包括的コードレビュー報告書

**レビュー日:** 2026-03-20
**対象:** コードベース全体（Sprint 0〜14 完了後）
**テスト結果:** 全 339 件パス（C# 151 + JS 188）

---

## 総合評価

全体的に**非常に高品質**なコードベース。クリーンアーキテクチャの4層構造（Domain → Application → Infrastructure → Functions）は依存ルールを厳守しており、CLAUDE.md に定義されたコーディング規約への準拠率も高い。前回レビュー（Sprint 時点）で指摘された主要な問題（N+1 クエリ、UseCase インターフェース欠如、TimeProvider 未導入、LIKE エスケープ漏れ等）は**すべて修正済み**。

以下、現時点で残存する問題を重要度順に整理する。

---

## 1. CRITICAL（重大な問題）

### 1.1 Fire-and-forget による動画ダウンロード実行

**該当:** [`src/api/XVideoCollector.Functions/Functions/VideoFunctions.cs:36`](../src/api/XVideoCollector.Functions/Functions/VideoFunctions.cs#L36)

```csharp
_ = Task.Run(() => downloadVideo.ExecuteAsync(video.Id, CancellationToken.None), CancellationToken.None);
```

**問題:**
- Azure Functions Consumption Plan ではホストプロセスが関数実行完了後に回収される可能性がある
- `Task.Run` で起動した非同期処理は追跡されず、例外が握りつぶされる
- ダウンロード失敗時に Video が `Downloading` / `Processing` 状態のまま永久に放置される
- CLAUDE.md 明記のルール「Azure Functions Consumption Plan では fire-and-forget 禁止」に違反

**推奨:** Azure Storage Queue Trigger を使い、登録後にキューメッセージを発行 → 別の Function でダウンロードを処理するアーキテクチャに変更。

> **→ 改善提案:** [Sprint 15.1 — Queue Trigger による動画ダウンロード非同期化](improvement-proposal.md#151-queue-trigger-による動画ダウンロード非同期化)

### 1.2 IUnitOfWork パターンの未活用（Repository 内での SaveChangesAsync 直接呼び出し）

**該当:** 全 Repository 実装

| ファイル | 該当行 |
|---------|--------|
| [`VideoRepository.cs`](../src/api/XVideoCollector.Infrastructure/Repositories/VideoRepository.cs#L97) | L97, L103, L112 |
| [`TagRepository.cs`](../src/api/XVideoCollector.Infrastructure/Repositories/TagRepository.cs#L49) | L49, L55, L64 |
| [`CategoryRepository.cs`](../src/api/XVideoCollector.Infrastructure/Repositories/CategoryRepository.cs#L19) | L19, L25, L34 |
| [`VideoTagRepository.cs`](../src/api/XVideoCollector.Infrastructure/Repositories/VideoTagRepository.cs#L20) | L20, L29, L42, L60 |

```csharp
// 例: VideoRepository.AddAsync
public async Task AddAsync(Video video, CancellationToken ct)
{
    db.Videos.Add(video);
    await db.SaveChangesAsync(ct);  // ← Repository 内で個別にコミット
}
```

**問題:**
- CLAUDE.md 明記のルール「Repository 内で個別に SaveChangesAsync しない」に違反
- `IUnitOfWork` インターフェースは定義・DI 登録済みだが、どの UseCase からも利用されていない
- `UpdateVideoUseCase` や `DeleteVideoUseCase` のような複数リポジトリにまたがる操作がトランザクション保護されない
- 途中で例外が発生した場合、データの不整合が生じる可能性がある

**推奨:**
1. Repository から `SaveChangesAsync` 呼び出しを削除
2. UseCase の末尾で `IUnitOfWork.SaveChangesAsync()` を呼び出す
3. `DownloadVideoUseCase` のように中間状態の保存が必要な場合は、状態遷移ごとに `SaveChangesAsync` を呼ぶ設計を明示的に文書化

> **→ 改善提案:** [Sprint 15.2 — IUnitOfWork パターンの実活用](improvement-proposal.md#152-iunitofwork-パターンの実活用)

### 1.3 SQL パスワードの Bicep 出力への露出

**該当:** [`infra/modules/functions.bicep:64`](../infra/modules/functions.bicep#L64)（接続文字列の appSettings 設定）

**問題:**
- SQL 接続文字列が Bicep の `output` として出力され、平文パスワードが ARM デプロイ履歴に記録される
- Static Web Apps の API キーも同様に Bicep 出力として露出している

**推奨:** Azure Key Vault にシークレットを格納し、Functions App からは Key Vault 参照 (`@Microsoft.KeyVault(...)`) で取得。または Managed Identity + RBAC による認証に切り替え。

> **→ 改善提案:** [Sprint 15.3 — シークレット管理の Key Vault 移行](improvement-proposal.md#153-シークレット管理の-key-vault-移行)

---

## 2. HIGH（高優先度）

### 2.1 Functions アプリへの直接アクセスに対する認証防御なし

**該当:** 全 HTTP Trigger 関数（`AuthorizationLevel.Anonymous`）

| ファイル | 該当行 |
|---------|--------|
| [`VideoFunctions.cs`](../src/api/XVideoCollector.Functions/Functions/VideoFunctions.cs#L26) | L26, L49, L61, L74, L89, L99, L118 |
| [`TagFunctions.cs`](../src/api/XVideoCollector.Functions/Functions/TagFunctions.cs#L15) | L15, L24, L40, L54 |
| [`CategoryFunctions.cs`](../src/api/XVideoCollector.Functions/Functions/CategoryFunctions.cs#L14) | L14, L23, L39, L53 |

**問題:** SWA プロキシを経由せずに Functions アプリのエンドポイントに直接アクセスした場合、認証が一切適用されない。Functions アプリは独自のパブリック URL を持つ。

**推奨:** ExceptionMiddleware と同様のミドルウェアで `X-MS-CLIENT-PRINCIPAL` ヘッダーを検証するか、`AuthorizationLevel.Function` に変更して API キー認証を追加。

> **→ 改善提案:** [Sprint 16.1 — Functions 直接アクセス防御](improvement-proposal.md#161-functions-直接アクセス防御)

### 2.2 Bicep の .NET バージョン不一致

**該当:** [`infra/modules/functions.bicep:33`](../infra/modules/functions.bicep#L33)

```bicep
netFrameworkVersion: 'v9.0'  // ← .NET 9
```

**問題:** プロジェクトは `net10.0` をターゲットにしているが、Bicep テンプレートは .NET 9 を指定。デプロイ後にランタイム互換性の問題が発生する。

> **→ 改善提案:** [Sprint 18.1 — Bicep の修正](improvement-proposal.md#181-bicep-の修正)

### 2.3 CI の build-functions ジョブで `--no-restore` 使用

**該当:** [`.github/workflows/ci.yml:91`](../.github/workflows/ci.yml#L91)（publish コマンド）、L37-40（build コマンド）、L44-47（test コマンド）

**問題:** `build-functions` ジョブは別のランナーで実行されるため、`dotnet-test` ジョブでリストアされたパッケージは共有されない。`--no-restore` フラグにより NuGet パッケージの取得がスキップされ、ビルドが失敗する可能性がある。

> **→ 改善提案:** [Sprint 18.2 — CI パイプライン修正](improvement-proposal.md#182-ci-パイプライン修正)

### 2.4 staticwebapp.config.json のテナント ID プレースホルダー

**該当:** [`src/frontend/staticwebapp.config.json:27`](../src/frontend/staticwebapp.config.json#L27)

```json
"openIdIssuer": "https://login.microsoftonline.com/__TENANT_ID__/v2.0"
```

**問題:** `__TENANT_ID__` の置換が CI/CD パイプラインで自動化されていない。`setup.sh` では手動置換されるが、新規環境構築時に漏れる可能性がある。

> **→ 改善提案:** [Sprint 18.1 — Bicep の修正](improvement-proposal.md#181-bicep-の修正)（テナント ID 自動置換）

### 2.5 BlobStorageService での DateTimeOffset.UtcNow 直接使用

**該当:** [`src/api/XVideoCollector.Infrastructure/Services/BlobStorageService.cs:59`](../src/api/XVideoCollector.Infrastructure/Services/BlobStorageService.cs#L59)

```csharp
DateTimeOffset.UtcNow.Add(expiry)
```

**問題:** CLAUDE.md のルール「DateTimeOffset.UtcNow を直接呼ばず、TimeProvider を DI 経由で注入」に違反。SAS URL の有効期限がテスト時に制御できない。

---

## 3. MEDIUM（中優先度）

### 3.1 ExceptionMiddleware の文字列ベース例外分類

**該当:** [`src/api/XVideoCollector.Functions/Middleware/ExceptionMiddleware.cs:41`](../src/api/XVideoCollector.Functions/Middleware/ExceptionMiddleware.cs#L41)

```csharp
InvalidOperationException ioe when ioe.Message.Contains("not found")
    => (StatusCodes.Status404NotFound, ioe.Message),
```

**問題:** `InvalidOperationException` のメッセージ文字列に "not found" が含まれるかで 404 判定を行っている。メッセージの文言変更やローカライゼーションで壊れる。

**推奨:** `KeyNotFoundException` や専用の `NotFoundException` を定義して使用。

> **→ 改善提案:** [Sprint 16.3 — 例外処理の改善](improvement-proposal.md#163-例外処理の改善)

### 3.2 pageSize の上限チェックなし

**該当:**

| ファイル | 該当行 | 内容 |
|---------|--------|------|
| [`ListVideosUseCase.cs:17`](../src/api/XVideoCollector.Application/UseCases/ListVideosUseCase.cs#L17) | L17 | `if (pageSize < 1) pageSize = 20;` |
| [`SearchVideosUseCase.cs:24`](../src/api/XVideoCollector.Application/UseCases/SearchVideosUseCase.cs#L24) | L24 | `var pageSize = request.PageSize < 1 ? 20 : request.PageSize;` |

**問題:** `pageSize < 1` の下限チェックはあるが上限がない。`pageSize=100000` のようなリクエストでサーバーサイドページングが実質無効化される。

**推奨:** `pageSize = Math.Min(pageSize, 100)` のようなキャップを設定。

> **→ 改善提案:** [Sprint 16.2 — 入力バリデーション強化](improvement-proposal.md#162-入力バリデーション強化)

### 3.3 NuGet パッケージのワイルドカードバージョン指定

**該当:** [`src/api/XVideoCollector.Functions/XVideoCollector.Functions.csproj:12-14`](../src/api/XVideoCollector.Functions/XVideoCollector.Functions.csproj#L12)

```xml
<PackageReference Include="Microsoft.Azure.Functions.Worker" Version="*" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="*" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="*" />
```

**問題:** ビルドの再現性が保証されない。パッチやメジャーバージョンアップで意図しない破壊的変更が入る可能性がある。

> **→ 改善提案:** [Sprint 18.1 — Bicep の修正](improvement-proposal.md#181-bicep-の修正)（NuGet ピン留め含む）

### 3.4 JSON シリアライズ設定の二重管理

**該当:**

| ファイル | 該当行 |
|---------|--------|
| [`Program.cs:19-23`](../src/api/XVideoCollector.Functions/Program.cs#L19) | `JsonOptions` 設定（ASP.NET Core） |
| [`FunctionHelper.cs:9-13`](../src/api/XVideoCollector.Functions/Helpers/FunctionHelper.cs#L9) | `JsonSerializerOptions` 定義 |
| [`ExceptionMiddleware.cs:13-16`](../src/api/XVideoCollector.Functions/Middleware/ExceptionMiddleware.cs#L13) | 独立した `JsonSerializerOptions` |

**問題:** 3箇所で独立して `JsonSerializerOptions` が定義されている。新しいコンバーター追加時に一部だけ更新する不整合リスクがある。

> **→ 改善提案:** [Sprint 19.4 — JSON シリアライズ設定の一元管理](improvement-proposal.md#194-json-シリアライズ設定の一元管理)

### 3.5 DurationSeconds が常に 0

**該当:** [`src/api/XVideoCollector.Infrastructure/Services/YtDlpDownloadService.cs:179`](../src/api/XVideoCollector.Infrastructure/Services/YtDlpDownloadService.cs#L179)

```csharp
DurationSeconds: 0,  // ffprobe 連携が必要な場合は別途実装
```

**問題:** フロントエンドで `formatDuration(0)` が "00:00" と表示され、ユーザーに誤解を与える。

> **→ 改善提案:** [Sprint 19.1 — 動画再生時間の取得](improvement-proposal.md#191-動画再生時間の取得)

### 3.6 コンテンツタイプのハードコード

**該当:** [`src/api/XVideoCollector.Infrastructure/Services/BlobStorageService.cs`](../src/api/XVideoCollector.Infrastructure/Services/BlobStorageService.cs#L24)

| 行 | ハードコード値 |
|----|--------------|
| L24 | `"video/mp4"` |
| L30 | `"image/jpeg"` |

**問題:** `video/mp4` と `image/jpeg` がハードコードされている。yt-dlp が webm など他の形式でダウンロードした場合にコンテンツタイプが不正になる。

> **→ 改善提案:** [Sprint 19.2 — コンテンツタイプの動的判定](improvement-proposal.md#192-コンテンツタイプの動的判定)

### 3.7 VideoDto で内部 BlobPath を露出

**該当:** [`src/api/XVideoCollector.Application/Dtos/VideoDto.cs:12-13`](../src/api/XVideoCollector.Application/Dtos/VideoDto.cs#L12)

```csharp
string? BlobPath,
string? ThumbnailBlobPath,
```

**問題:** `BlobPath` と `ThumbnailBlobPath` は Azure Blob Storage の内部パス。クライアントに漏洩すると内部構造が推測可能。ストリーミング用の SAS URL に変換して返すべき。

### 3.8 FfmpegThumbnailService と YtDlpDownloadService が public

**該当:**

| ファイル | 該当行 |
|---------|--------|
| [`FfmpegThumbnailService.cs:9`](../src/api/XVideoCollector.Infrastructure/Services/FfmpegThumbnailService.cs#L9) | `public sealed class FfmpegThumbnailService` |
| [`YtDlpDownloadService.cs:10`](../src/api/XVideoCollector.Infrastructure/Services/YtDlpDownloadService.cs#L10) | `public sealed class YtDlpDownloadService` |

**問題:** 他の Infrastructure 実装クラスは `internal sealed` だが、この2つは `public` になっている。`InternalsVisibleTo` が設定されているため `internal` で問題ない。

---

## 4. LOW（低優先度・改善提案）

### 4.1 Tag / Category の Name にバリデーション不足

**該当:**
- [`Tag.cs:27`](../src/api/XVideoCollector.Domain/Entities/Tag.cs#L27) — `ArgumentException.ThrowIfNullOrWhiteSpace` のみ（長さ制限なし）
- [`Tag.cs:35`](../src/api/XVideoCollector.Domain/Entities/Tag.cs#L35) — Update メソッドも同様

**問題:** `VideoTitle` は最大200文字の制約があるが、Tag と Category の Name には長さ制限がない。

> **→ 改善提案:** [Sprint 16.2 — 入力バリデーション強化](improvement-proposal.md#162-入力バリデーション強化)

### 4.2 Category.Name に一意制約なし

**該当:** [`CategoryConfiguration.cs:17-19`](../src/api/XVideoCollector.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs#L17)

**問題:** Tag には `Name` の一意インデックスがあるが、Category にはない。同名カテゴリが作成可能。

> **→ 改善提案:** [Sprint 16.2 — 入力バリデーション強化](improvement-proposal.md#162-入力バリデーション強化)

### 4.3 VideoTagConfiguration に外部キー制約なし

**該当:** [`VideoTagConfiguration.cs:14-18`](../src/api/XVideoCollector.Infrastructure/Persistence/Configurations/VideoTagConfiguration.cs#L14)

**問題:** EF Core レベルでの参照整合性チェックやカスケード削除が未設定。DB マイグレーション時にデータ整合性問題の可能性がある。

> **→ 改善提案:** [Sprint 16.2 — 入力バリデーション強化](improvement-proposal.md#162-入力バリデーション強化)

### 4.4 RegisterVideoUseCase で重複チェックなし

**該当:** [`RegisterVideoUseCase.cs:13-26`](../src/api/XVideoCollector.Application/UseCases/RegisterVideoUseCase.cs#L13)

**問題:** 同じ TweetUrl の動画を複数回登録可能。DB レベルでの一意制約が設定されているかは EF Core 設定次第。

### 4.5 GetAllAsync メソッドの存在

**該当:** [`VideoRepository.cs:13-16`](../src/api/XVideoCollector.Infrastructure/Repositories/VideoRepository.cs#L13)

```csharp
public async Task<IReadOnlyList<Video>> GetAllAsync(CancellationToken cancellationToken = default)
    => await db.Videos
        .OrderByDescending(v => v.CreatedAt)
        .ToListAsync(cancellationToken);
```

**問題:** ページネーション付きの代替メソッドが存在する中で、全件取得メソッドも残っている。Tag や Category のように件数が少ないものは許容範囲だが、Video の `GetAllAsync` はデータ量増大時に問題になりうる。

### 4.6 フロントエンドの tagIds/tags パラメータ名不一致

**該当:** [`src/frontend/js/pages/videoList.js`](../src/frontend/js/pages/videoList.js)

| 行 | 内容 |
|----|------|
| L164 | API 呼び出しで `tagIds` パラメータを使用 |
| L184 | URL からクエリパラメータを `tags` として読み取り |
| L285 | URL パラメータを `tags` として設定 |

**問題:** 内部コードでは `tagIds`、URL クエリパラメータでは `tags` と命名が不統一。フィルタ状態のURL永続化時に不整合の可能性。

> **→ 改善提案:** [Sprint 19.3 — フロントエンド改善](improvement-proposal.md#193-フロントエンド改善)

### 4.7 フロントエンドのレースコンディション

**該当:** [`src/frontend/js/pages/videoList.js`](../src/frontend/js/pages/videoList.js) — ファイル全体に `AbortController` の使用なし

**問題:** ページネーションやフィルタ変更時に AbortController が使われておらず、前のリクエストの結果が後のリクエストの結果を上書きする可能性がある。

> **→ 改善提案:** [Sprint 19.3 — フロントエンド改善](improvement-proposal.md#193-フロントエンド改善)

---

## 5. CLAUDE.md 準拠状況

### バックエンド (C# / .NET 10)

| 規約 | 状態 | 備考 |
|------|------|------|
| Nullable reference types 有効 | PASS | 全プロジェクトで `<Nullable>enable</Nullable>` |
| file-scoped namespace | PASS | 全ファイルで使用 |
| primary constructor | PASS | 全 UseCase、Repository、Service で活用 |
| record を DTO / 値オブジェクトに使用 | PASS | 全 DTO、TweetUrl、BlobPath、VideoTitle |
| sealed クラス | PASS | 全エンティティ、UseCase、Service |
| Async サフィックス | PASS | 全非同期メソッド |
| UseCase インターフェース定義 | PASS | 全9 UseCase にインターフェース定義あり |
| DI コンテナにインターフェース経由で登録 | PASS | `DependencyInjection.cs` で確認 |
| CreatedAt / UpdatedAt 監査プロパティ | PASS | 全エンティティに存在 |
| TimeProvider を DI 経由で注入 | PARTIAL | BlobStorageService のみ `DateTimeOffset.UtcNow` 直接使用 |
| IUnitOfWork パターン | FAIL | 定義はあるが未活用、Repository 内で個別 SaveChanges |
| LIKE ワイルドカードエスケープ | PASS | `VideoRepository` で `%`, `_`, `[` をエスケープ |
| fire-and-forget 禁止 | FAIL | `VideoFunctions.cs` で `Task.Run` 使用 |

### フロントエンド (Vanilla JS)

| 規約 | 状態 | 備考 |
|------|------|------|
| ES Modules | PASS | 全ファイルで import/export |
| const 優先、var 禁止 | PASS | var 使用なし |
| innerHTML 禁止 | PASS | 全ファイルで createElement + textContent |
| async/await（.then 禁止） | PASS | 全非同期処理で async/await |
| インラインスタイル禁止 | PASS | CSS クラスで定義 |
| CSS カスタムプロパティ | PASS | variables.css でテーマ管理 |
| デスクトップファーストレスポンシブ | PASS | メディアクエリで確認 |

---

## 6. テスト品質評価

### テスト概要

| レイヤー | テスト数 | 状態 |
|----------|---------|------|
| Domain | 51 | 全パス |
| Application | 35 | 全パス |
| Infrastructure | 42 | 全パス |
| Functions | 23 | 全パス |
| Frontend (JS) | 188 | 全パス |
| **合計** | **339** | **全パス** |

### 強み

- Domain 層の状態遷移テストが充実（正常系・異常系を網羅）
- 値オブジェクトの構造的等価性テストあり
- セキュリティテスト（yt-dlp コマンドインジェクション防止）が6パターン
- Theory + InlineData によるパラメタライズドテスト活用
- フロントエンドでアクセシビリティテスト（role, tabindex, keyboard navigation）
- AAA パターンおおむね遵守
- Moq のインスタンス化はコンストラクタ内（static readonly 共有なし）

### カバレッジギャップ

| 未テスト項目 | 重要度 |
|-------------|--------|
| Category エンティティ（テストファイルなし） | HIGH |
| GetVideoUseCase（専用テストファイルなし） | MEDIUM |
| DownloadVideoUseCase（専用テストファイルなし、Functions テスト経由のみ） | MEDIUM |
| BlobStorageService（テストファイルなし、モックのみ） | MEDIUM |
| TagRepository / VideoTagRepository（テストファイルなし） | MEDIUM |
| VideoRepository の GetPagedAsync / SearchPagedAsync | MEDIUM |
| UpdatedAt 監査プロパティの更新テスト | LOW |
| IUnitOfWork のトランザクション動作テスト | LOW |
| Video に BlobPath がある場合の DeleteVideoUseCase のBlobクリーンアップ | LOW |
| E2E / 統合テスト全般 | LOW |

### 改善点

- DeleteVideoUseCase: テスト2件のみで、BlobPath ありの動画削除時の Blob クリーンアップ検証がない
- UpdateVideoUseCase: テスト2件のみで、空タイトル・無効タグ ID などのエッジケースがない
- InMemoryDatabase によるリポジトリテストは SQL Server 固有の動作（LIKE、OFFSET/FETCH）を検証できない
- `api.test.js` で `global.fetch` モックの afterEach クリーンアップがない

---

## 7. アーキテクチャ評価

### クリーンアーキテクチャ準拠

| 観点 | 評価 |
|------|------|
| 依存方向（外→内の一方向） | 完全準拠 |
| Domain 層の外部パッケージ依存ゼロ | 完全準拠 |
| Repository インターフェースの Domain 層定義 | 完全準拠 |
| Infrastructure 具象クラスの隠蔽（internal sealed） | ほぼ準拠（2 Service のみ public） |
| UseCase のインターフェース定義 | 完全準拠 |
| DI による依存性逆転 | 完全準拠 |

### セキュリティ

| 観点 | 評価 |
|------|------|
| XSS 防止（innerHTML 禁止） | 完全準拠 |
| コマンドインジェクション防止（yt-dlp URL 検証） | 優秀 |
| LIKE ワイルドカードエスケープ | 完全準拠 |
| SAS URL による Blob アクセス制御 | 適切 |
| TLS 1.2 最小、HTTPS 強制 | 適切（Bicep 全リソース） |
| Blob パブリックアクセス無効化 | 適切 |
| OIDC による Azure デプロイ認証 | 優秀 |
| Functions 直接アクセス防御 | 未実装（要改善） |
| シークレット管理（Key Vault） | 未実装（要改善） |

---

## 8. まとめ

| 優先度 | 件数 | 主な項目 |
|--------|------|----------|
| CRITICAL | 3 | Fire-and-forget、UnitOfWork 未活用、SQL パスワード露出 |
| HIGH | 5 | Functions 認証防御、.NET バージョン不一致、CI ビルド問題、テナント ID 置換、TimeProvider 違反 |
| MEDIUM | 8 | 例外分類、pageSize 上限、NuGet ワイルドカード、JSON 二重管理、DurationSeconds、コンテンツタイプ等 |
| LOW | 7 | Name バリデーション、一意制約、外部キー、重複チェック、GetAllAsync 等 |

前回レビューと比較すると、指摘事項の多くが修正されており（UseCase インターフェース追加、TimeProvider 導入、N+1 解消、LIKE エスケープ実装等）、コードベースの品質は着実に向上している。残る課題は主にインフラ/デプロイ層のセキュリティとアーキテクチャ改善（Queue Trigger 化、UnitOfWork 活用、Key Vault 導入）に集中している。
