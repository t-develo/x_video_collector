# 包括的コードレビュー

**レビュー日:** 2026-03-20
**対象:** コードベース全体（main ブランチ, commit 4be976b）
**テスト結果:** 全 182 件パス（C# 87 + JS 95）

## 総合評価

全体的に**高品質**なコードベース。クリーンアーキテクチャの依存ルールは厳守されており、命名規約・file-scoped namespace・nullable reference types など CLAUDE.md の規約にも準拠。以下、改善が必要な点を重要度順にまとめる。

---

## CRITICAL（重大な問題）

### 1. Fire-and-forget による例外の握りつぶし

**該当:** `VideoFunctions.cs:37`

```csharp
_ = Task.Run(() => downloadVideo.ExecuteAsync(video.Id, CancellationToken.None), CancellationToken.None);
```

**問題:** `Task.Run` で発生した例外が完全に無視される。ダウンロード失敗時にログも残らず、Video が `Downloading` 状態のまま永久に放置される。Azure Functions Consumption プランでは関数終了後にプロセスが回収される可能性があり、バックグラウンドタスクの完了が保証されない。

**推奨:** Queue Trigger（Azure Storage Queue や Service Bus）を使い、登録後にメッセージを発行 → 別の Function でダウンロードを処理するアーキテクチャに変更すべき。暫定対処としては最低限 `.ContinueWith` でエラーログを出すべき。

### 2. LIKE ワイルドカード文字のエスケープ漏れ

**該当:** `VideoRepository.cs:25`

```csharp
q = q.Where(v => EF.Functions.Like(v.Title.Value, $"%{query.Keyword}%"));
```

**問題:** EF Core の LIKE はパラメータ化されるため SQL インジェクションにはならないが、`%` や `_` などの LIKE ワイルドカード文字がエスケープされていない。ユーザーが `%` を含む検索語を入力すると予期しないマッチが発生する。

**推奨:** `query.Keyword.Replace("%", "[%]").Replace("_", "[_]")` でエスケープするか、`EF.Functions.Contains` を使用。

### 3. N+1 クエリ問題

**該当:** `ListVideosUseCase.cs:18-33`, `SearchVideosUseCase.cs:22-41`

```csharp
var allVideos = await videoRepository.GetAllAsync(cancellationToken);  // 全件取得
// ...
foreach (var video in paged) {
    var tags = await tagRepository.GetByVideoIdAsync(video.Id, cancellationToken);  // N回クエリ
}
```

**問題:**

- `GetAllAsync` で**全件**をメモリにロードしてからアプリ側でページングしている（DB レベルでの `OFFSET`/`FETCH` を活用していない）
- さらに各動画ごとにタグを個別取得（N+1 問題）
- データが増えるとパフォーマンスが劣化する

**推奨:** リポジトリにページング付きメソッド（`Task<(IReadOnlyList<Video>, int)> GetPagedAsync(int skip, int take)`）を追加。タグは `Include` / `JOIN` で一括取得。

---

## HIGH（高優先度）

### 4. トランザクション境界の欠如

**該当:** `UpdateVideoUseCase.cs`, `DeleteVideoUseCase.cs`

`UpdateVideoUseCase` では：

1. 既存タグを全削除 (`DeleteByVideoIdAsync`)
2. 新タグを1件ずつ追加（ループ内で毎回 `SaveChangesAsync`）
3. ビデオ本体を更新

各操作が個別の `SaveChangesAsync` で実行されるため、途中で例外が発生すると不整合が生じる。

**推奨:** Repository 内で毎回 `SaveChangesAsync` するのではなく、UseCase 側で Unit of Work パターンを使い、1トランザクションで処理すべき。または `IUnitOfWork` を導入。

### 5. UseCase が `class` で `virtual` — インターフェース未定義

**該当:** 全 UseCase クラス

```csharp
public class RegisterVideoUseCase(IVideoRepository videoRepository)
{
    public virtual async Task<VideoDto> ExecuteAsync(...)
```

**問題:** テスト時に Mock するために `virtual` を付けている設計だが、本来インターフェース（`IRegisterVideoUseCase`）を定義して DI すべき。`sealed` でない `class` + `virtual` は意図せぬ継承のリスクがあり、クリーンアーキテクチャの「依存性逆転の原則」にも反する。

**推奨:** UseCase ごとにインターフェース（`IUseCase<TRequest, TResponse>` 等）を定義し、DI コンテナにはインターフェース経由で登録。Functions 層ではインターフェースに依存させる。

### 6. `Category.Update` / `Tag.Update` で `UpdatedAt` が更新されない

**該当:** `Category.cs:28-34`, `Tag.cs:30-36`

```csharp
public void Update(string name, int sortOrder)
{
    Name = name.Trim();
    SortOrder = sortOrder;
    // UpdatedAt の更新がない
}
```

`Video` エンティティでは全 mutator で `UpdatedAt = DateTimeOffset.UtcNow` をしているが、`Category` と `Tag` には `UpdatedAt` プロパティ自体がない。エンティティ間で監査情報の一貫性が欠けている。

### 7. `DateTimeOffset.UtcNow` のハードコード

**該当:** `Video.cs:50`, 各エンティティ

テスト時に時刻を制御できない。t\_wada 式 TDD の観点から、**時刻は外部から注入すべき**。

**推奨:** `IClock` インターフェース（`TimeProvider` in .NET 8+）を導入し、エンティティのファクトリまたは UseCase に注入。

---

## MEDIUM（中優先度）

### 8. 不要なテストファイルの残骸

**該当:** `UnitTest1.cs`

```csharp
public class UnitTest1
{
    [Fact]
    public void Test1() { }
}
```

テンプレート生成時の残骸。空テストは「テストが通っている」という誤った安心感を与える。削除すべき。

### 9. `ReadBodyAsync` の重複（DRY 原則違反）

**該当:** `VideoFunctions.cs:132-142`, `CategoryFunctions.cs:66-76`, `TagFunctions.cs:68-78`

同じ `ReadBodyAsync<T>` と `JsonOptions` が 3 ファイルに重複している。

**推奨:** 共通の `FunctionHelper` static クラスに抽出。

### 10. `JsonSerializerOptions` の不統一

- `VideoFunctions.cs:21-24`: `PropertyNameCaseInsensitive = true` のみ
- `TagFunctions.cs:13-17`: 上記 + `JsonStringEnumConverter`
- `Program.cs:19-23`: `CamelCase` + `JsonStringEnumConverter`

Enum のシリアライズが `VideoFunctions` と `CategoryFunctions` では文字列にならない可能性がある。

### 11. フロントエンドで全件取得

**該当:** `videoList.js:220`

```javascript
const result = await api.get('/videos?page=1&pageSize=1000');
```

1000 件ハードコードで全件取得し、クライアント側でソート・ページングしている。データ量が増えるとブラウザのメモリとネットワークに問題が出る。サーバーサイドページングを活用すべき。

### 12. `innerHTML = ''` の使用（CLAUDE.md 規約違反）

**該当:** `router.js:41`, `router.js:71`

```javascript
container.innerHTML = '';
```

CLAUDE.md では `innerHTML 禁止（XSS 防止）` と明記されている。空文字列代入なので実害はないが規約違反。`clearChildren(container)` が `dom.js` に既に存在するので、そちらを使うべき。

### 13. `render404` でインラインスタイル

**該当:** `router.js:74-77`

```javascript
wrapper.style.cssText = 'text-align:center;padding:4rem 2rem;';
code.style.cssText = 'font-family:var(--font-mono);font-size:4rem;...';
```

CSS は CSS ファイルに分離すべき（関心の分離）。

### 14. `BlobStorageService.GetSasUrlAsync` が同期的

**該当:** `BlobStorageService.cs:48-62`

```csharp
public Task<string> GetSasUrlAsync(...) {
    return Task.FromResult(sasUri.ToString());
}
```

`Task.FromResult` で同期処理を非同期に偽装している。インターフェースが非同期なので仕方ない面もあるが、`ValueTask<string>` の使用を検討。

### 15. `DurationSeconds` が常に 0

**該当:** `YtDlpDownloadService.cs:179`

```csharp
DurationSeconds: 0,  // ffprobe 連携が必要な場合は別途実装
```

コメントで「別途実装」とあるが、実装されないままリリースコードに含まれている。フロントエンドで `formatDuration(0)` が `"00:00"` と表示され、ユーザーを混乱させる。

---

## LOW（低優先度・改善提案）

### 16. `VideoTag` が `record` だが EF Core エンティティ

**該当:** `VideoTag.cs`

```csharp
public sealed record VideoTag(Guid VideoId, Guid TagId);
```

`record` は値の等価性で比較されるため、EF Core のトラッキングとの相性に注意。現時点では問題ないが、将来的にプロパティ追加時にハマる可能性がある。

### 17. `Category.Name` / `Tag.Name` のバリデーション不足

`Category.Create` では `ThrowIfNullOrWhiteSpace` のみで、最大長のチェックがない。`VideoTitle` は `MaxLength = 200` を定義しているのに、Category と Tag の Name には制限がない。

### 18. テスト名と内容の乖離

**該当:** `YtDlpDownloadServiceTests.cs:78-93`

`DownloadAsync_TimeoutExpires_ThrowsTimeoutException` が実際にはタイムアウトをテストしておらず、`Assert.Equal(1, opts.TimeoutSeconds)` で設定値を確認しているだけ。テスト名と内容が乖離している。

### 19. `createUserInfo` で `async/await` を使わず `.then` チェーン

**該当:** `header.js:44-56`

CLAUDE.md の規約「すべての非同期処理は `async`/`await`」に違反。

### 20. Application 層の DI パッケージ依存

**該当:** `Application/DependencyInjection.cs`

クリーンアーキテクチャの純粋な解釈では Application 層がフレームワーク固有の `Microsoft.Extensions.DependencyInjection` パッケージに依存するのは望ましくない。ただし実用上は許容範囲。

---

## テスト品質（t\_wada 式 TDD 観点）

### 良い点

- Domain 層のテストが充実（状態遷移の正常/異常系を網羅）
- 値オブジェクトの等価性テストあり
- セキュリティ（コマンドインジェクション）のテストあり
- xUnit の `Theory` + `InlineData` でパラメタライズドテスト活用

### 改善点

- **テストの独立性:** `VideoFunctionsTests` で `static readonly Mock` を使っている（`VideoFunctionsTests.cs:17-22`）。テスト間で状態が共有される危険。各テストメソッド内でインスタンス化すべき
- **Arrange-Act-Assert の明示:** AAA パターンはおおむね守られているが、空行での区切りが不統一
- **テストダブルの選択:** Moq の `Setup` 未設定でデフォルト値を返す暗黙の挙動に依存している箇所が多い。意図を明示すべき
- **エッジケースの不足:** ページネーションの境界値テスト（0件、1件、ちょうど pageSize 件）が少ない

---

## クリーンアーキテクチャ観点

### 良い点

- 依存方向は完全に 外→内 で正しい
- Domain 層は外部パッケージ参照ゼロ（csproj 確認済み）
- Repository インターフェースは Domain 層に定義
- Infrastructure の具象クラスは `internal sealed` で隠蔽

### 改善点

- UseCase にインターフェースがなく、Functions 層が具象クラスに直接依存
- `VideoSearchQuery` record が `IVideoRepository.cs` に同居しており、別ファイルに分離すべき
- Application 層の `VideoMapper` は `internal static` だがテストから参照できない。マッピングロジックのテストが欠落

---

## まとめ（対応優先度）

| 優先度 | 件数 | 主な項目 |
|--------|------|----------|
| CRITICAL | 3 | Fire-and-forget, LIKE エスケープ, N+1 |
| HIGH | 4 | トランザクション欠如, UseCase インターフェース, UpdatedAt 不整合, 時刻注入 |
| MEDIUM | 8 | UnitTest1 残骸, ReadBody 重複, JSON 不統一, innerHTML, 全件取得 等 |
| LOW | 5 | record エンティティ, Name バリデーション, テスト命名, async/await 規約 等 |
