# コードレビューレポート — 2026-03-23

レビュー対象: `/home/user/x_video_collector` 全ソースコード
レビュー日時: 2026-03-23
レビュアー: Code Reviewer Agent (claude-sonnet-4-6)

---

## Code Review Report

### CRITICAL

なし — CRITICAL 相当の問題は検出されませんでした。

---

### HIGH

#### アーキテクチャ

- [x] `/home/user/x_video_collector/src/api/XVideoCollector.Infrastructure/Services/BlobStorageService.cs:65` — `DateTimeOffset.UtcNow` の直接使用
  `GetSasUrlAsync` メソッド内の `DateTimeOffset.UtcNow.Add(expiry)` が `TimeProvider` を経由せず直接 `DateTimeOffset.UtcNow` を呼んでいる。このクラスは `TimeProvider` を DI 注入していないため、SAS URL の有効期限がテストで制御不能になる。
  **修正方法**: コンストラクタで `TimeProvider` を受け取り、`timeProvider.GetUtcNow().Add(expiry)` に変更する。

#### コード品質

- [x] `/home/user/x_video_collector/tests/XVideoCollector.Infrastructure.Tests/Services/YtDlpDownloadServiceTests.cs:8` — テストクラスが `sealed` でない
  `public class YtDlpDownloadServiceTests` が `sealed` になっていない。CLAUDE.md ではクラスを原則 `sealed` にするよう定めている。

- [x] `/home/user/x_video_collector/tests/XVideoCollector.Infrastructure.Tests/Services/FfmpegThumbnailServiceTests.cs:8` — テストクラスが `sealed` でない
  `public class FfmpegThumbnailServiceTests` が `sealed` になっていない。同上。

- [x] `/home/user/x_video_collector/src/api/XVideoCollector.Application/UseCases/DownloadVideoUseCase.cs:27–99` — `ExecuteAsync` が 50 行を超えている（72 行）
  メソッド内に「ダウンロード」「Blob アップロード」「サムネイル生成」「状態遷移」という複数の責務が混在している。各フェーズをプライベートメソッドに切り出すことが望ましい。

- [x] `/home/user/x_video_collector/src/api/XVideoCollector.Functions/Functions/VideoFunctions.cs:140–183` — `SearchVideosAsync` が 43 行かつクエリパラメータ解析ロジックが肥大化
  tagIds のパース処理（164–170 行）は `FunctionHelper` などに切り出すべき。単体テストも困難になる。（50 行には収まっているが構造上の懸念として記録する。）

---

### LOW

#### テスト規約

- [ ] `/home/user/x_video_collector/tests/XVideoCollector.Application.Tests/UseCases/RegisterVideoUseCaseTests.cs:14–15` — `Mock` をフィールドとして共有している
  `_videoRepoMock` および `_unitOfWorkMock` がフィールドで初期化されており、コンストラクタで Setup なしに使われる。CLAUDE.md では "Mock は各テストメソッド内またはコンストラクタでインスタンス化する" とあるが、コンストラクタ内で直接 `new Mock<>()` としておりテスト間で状態が漏れる構造になっている（`xUnit` の場合、テストごとにクラスが再生成されるため実害はないが、一部の他テストクラスも同様のパターンを踏んでいる）。
  ただし xUnit ではインスタンスがテストごとに再生成されるため実際の問題はない。CLAUDE.md の文言に厳密に従うなら、コンストラクタ内で `new Mock<>()` を明示するのが最善。

- [ ] `/home/user/x_video_collector/tests/XVideoCollector.Infrastructure.Tests/Repositories/VideoRepositoryTests.cs:159–177` — テストメソッド名が `MethodName_Condition_ExpectedResult` パターンに完全準拠していない
  `GetPagedAsync_WithTitleAscSort_ReturnsTitleOrdered` は許容範囲だが `GetStatsAsync_WhenNoVideos_ReturnsZeroCounts` など一部メソッドは条件部が "When + 状態" 形式でパターンに沿っている。問題レベルではないが統一性の観点で記録する。

- [ ] `/home/user/x_video_collector/src/api/XVideoCollector.Functions/Functions/VideoFunctions.cs:121–138` — `GetVideoStreamUrlAsync` 内の非同期ラムダ（fire-and-forget ではない）
  この箇所は通常の `await` で呼び出されているため fire-and-forget には該当しない。問題なし。

- [ ] `/home/user/x_video_collector/src/frontend/js/pages/videoDetail.js:157–183` — `poll()` 関数が fire-and-forget で呼ばれている（行 182: `poll();`）
  `renderPendingSection` 内で `poll()` を `await` せず呼び出している。これは意図的な設計（DOM 接続チェックによるポーリング停止）であるが、ポーリング中に発生するエラーは外側に伝播しない。JS のフロントエンドコードであり、Azure Functions の fire-and-forget 禁止規約の対象外。情報として記録する。

- [ ] `/home/user/x_video_collector/src/frontend/js/pages/videoDetail.js:284–292` — 非同期 IIFE（SAS URL 取得）が fire-and-forget
  `(async () => { ... })()` で SAS URL を取得しているが、このパターンも `await` されていない。フロントエンド固有の UI 非同期処理のため規約違反ではないが、エラーが `playerStatus` テキスト更新以外に伝播しない点を認識しておく必要がある。

- [ ] `/home/user/x_video_collector/src/api/XVideoCollector.Application/Interfaces/*.cs` — 公開インターフェースに XML ドキュメントコメントがない
  `IRegisterVideoUseCase`、`IGetVideoUseCase`、`IListVideosUseCase` など Application 層の全インターフェースに `///` XML ドキュメントコメントがない。CLAUDE.md のベストプラクティスに該当する。

- [ ] `/home/user/x_video_collector/tests/XVideoCollector.Application.Tests/UseCases/SearchVideosUseCaseTests.cs:23` — テストメソッド名の条件部が不完全
  `ExecuteAsync_ReturnsPagedResults` は Condition 部分 (`When_xxx`) が欠落している。例: `ExecuteAsync_WithResults_ReturnsPagedResults` のほうが規約に沿う。

- [ ] `/home/user/x_video_collector/tests/XVideoCollector.Application.Tests/UseCases/DownloadVideoUseCaseTests.cs:42` — テストメソッド名のパターン違反
  `ExecuteAsync_NonExistingVideo_ThrowsInvalidOperationException` は適切だが、`ExecuteAsync_DownloadFails_MarksVideoAsFailed` は条件部（`When_DownloadFails`）が動詞形になっており規約上の `MethodName_Condition_ExpectedResult` から少し外れる。軽微。

---

## 詳細所見（コンテキスト補足）

### セキュリティ — 問題なし

**コマンドインジェクション（yt-dlp）**
`YtDlpDownloadService.ValidateUrl` にて URL スキーム・ホスト・禁止文字（`;`, `|`, `&`, `$`, `` ` ``, `'`）のバリデーションが実装されており、`AllowedHosts` ホワイトリストも適用されている。`Process.StartInfo.Arguments` への渡し方もダブルクォートでラップされている。インジェクションリスクは十分に緩和されている。

**SQL インジェクション**
全クエリが EF Core の LINQ 式または `EF.Functions.Like` で構成されており、文字列連結クエリは存在しない。`VideoRepository.ApplyFilters` 内の `EF.Functions.Like` は `%`, `_`, `[` の LIKE ワイルドカードエスケープ（`[%]`, `[_]`, `[[]`）が正しく実装されている。問題なし。

**XSS**
フロントエンド全体で `innerHTML` の使用はなく、すべて `textContent` または `document.createElement` + `appendChild` による安全な DOM 操作が行われている。`clearChildren()` も `innerHTML = ''` ではなく `removeChild` ループで実装されている。問題なし。

**ハードコード秘密情報**
`appsettings.json` の接続文字列は空文字列であり、実際の秘密情報はない。`BlobStorageOptions.ConnectionString` は構成から注入される設計になっている。問題なし。

**認証**
`AuthMiddleware` がすべての HTTP トリガー関数に適用されており、`X-MS-CLIENT-PRINCIPAL` ヘッダーで認証チェックを行っている。ヘルスチェックエンドポイント (`/api/health`) は意図的にスキップされており、設計として適切。

### アーキテクチャ — ほぼ問題なし

**依存ルール**
Domain → Application → Infrastructure → Functions の依存方向は全体的に正しく維持されている。`DeleteVideoUseCase`、`DownloadVideoUseCase` が Application 層から `IBlobStorageService`（Application.Services インターフェース）を通じて Blob 操作しており、Infrastructure の具象クラスへの直接参照は存在しない。

**IUnitOfWork パターン**
全リポジトリの `AddAsync`/`UpdateAsync`/`DeleteAsync` メソッドが `SaveChangesAsync` を呼ばず、UseCase 側で `unitOfWork.SaveChangesAsync` を呼ぶ設計が貫徹されている。問題なし。

**TimeProvider**
Domain エンティティ、Application UseCase、Infrastructure HealthCheckService すべてで `TimeProvider` が DI 注入されて使用されている。唯一の例外が `BlobStorageService.GetSasUrlAsync` における `DateTimeOffset.UtcNow` の直接使用（HIGH として記録済み）。

**Task.Run fire-and-forget**
Functions 層に `Task.Run` の放置は存在しない。`DownloadVideoQueueFunction` が Queue Trigger ベースで `await downloadVideo.ExecuteAsync(...)` を正しく実行している。問題なし。

### コード品質 — 全般良好

**sealed クラス**
Domain エンティティ（`Video`, `Tag`, `Category`）、Infrastructure サービス、UseCase クラスはすべて `sealed` が付与されている。2 つのテストクラスのみ未適用（HIGH に記録済み）。

**UpdatedAt**
`Video` の全状態変更メソッド（`StartDownloading`, `StartProcessing`, `MarkReady`, `MarkFailed`, `ResetToPending`, `UpdateTitle`, `SetCategory`, `UpdateNotes`）が `UpdatedAt = timeProvider.GetUtcNow()` を呼んでいる。`Tag.Update`、`Category.Update` も同様。問題なし。

**非同期サフィックス**
C# の全非同期メソッドに `Async` サフィックスが付与されている。問題なし。

**EF.Functions.Like エスケープ**
`VideoRepository.ApplyFilters` にて `[`, `%`, `_` の 3 文字がエスケープされている。問題なし。

### フロントエンド規約 — 問題なし

- `var` の使用なし（全て `const`/`let`）
- `.then()` チェーンなし（全て `async/await`）
- `innerHTML` の使用なし
- `el.style.xxx` インラインスタイルの使用なし
- `clearChildren()` の正しい使用が確認できる

### テスト — 全般良好

**AAA パターン**
`DownloadVideoUseCaseTests` の一部テストには `// Arrange / Act / Assert` コメントが付いており、他のテストも構造的に AAA に従っている。

**境界値テスト**
`SearchVideosUseCaseTests` に 0件/1件/ページ境界のテストが含まれており適切。

**FakeTimeProvider**
`VideoRepositoryTests` に `FakeTimeProvider` を定義して時刻依存テストを実装しており、設計が適切。

**BlobStorageServiceTests の懸念**
`BlobStorageServiceTests` はインターフェースのモックをそのまま Setup してテストしており、実装クラスを直接テストしていない。これはテストとして意味が薄い（モックが自分自身の期待値を検証しているだけ）。LOW レベルの情報として記録する。ただしこれは Azure Storage への接続が必要なため実環境テストが困難という現実的な制約の回避策と考えられる。

---

## 総合評価

### Verdict: Warning

**ブロッカーなし。** CRITICAL レベルの問題（ハードコード秘密情報、インジェクション脆弱性、XSS、fire-and-forget）は検出されなかった。

**修正推奨事項（優先度順）:**

1. **HIGH: `BlobStorageService.GetSasUrlAsync` の `DateTimeOffset.UtcNow` 直接使用**
   `/home/user/x_video_collector/src/api/XVideoCollector.Infrastructure/Services/BlobStorageService.cs:65`
   `TimeProvider` を DI 注入してテスト可能な設計に変更する。

2. **HIGH: テストクラス 2 件の `sealed` 修飾子漏れ**
   `/home/user/x_video_collector/tests/XVideoCollector.Infrastructure.Tests/Services/YtDlpDownloadServiceTests.cs:8`
   `/home/user/x_video_collector/tests/XVideoCollector.Infrastructure.Tests/Services/FfmpegThumbnailServiceTests.cs:8`

3. **HIGH: `DownloadVideoUseCase.ExecuteAsync` の関数長（72行）**
   `/home/user/x_video_collector/src/api/XVideoCollector.Application/UseCases/DownloadVideoUseCase.cs:27`
   Blob アップロードフェーズとサムネイルフェーズをプライベートメソッドに切り出す。

4. **LOW: Application 層インターフェースへの XML ドキュメントコメント追加**
   `/home/user/x_video_collector/src/api/XVideoCollector.Application/Interfaces/` 以下の全ファイル

5. **LOW: `BlobStorageServiceTests` の実装テスト強化**
   モックのみのテストから、InMemory Azure Storage エミュレーターまたは Azurite を使った統合テストへの移行を検討する。
