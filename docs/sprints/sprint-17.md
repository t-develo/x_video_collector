# Sprint 17: サーバーサイドソート & 統計 API

## 目的

現在クライアントサイドのみで実装されているソート機能をサーバーサイドに移行し、
ページをまたいだ正確なソートを実現する。また、動画コレクションの統計情報を返す
API エンドポイントを追加してダッシュボード用途に活用する。

## ブランチ

`feature/sprint17/sort-stats`

## 背景・問題点

| # | 問題 | 深刻度 |
|---|------|--------|
| 1 | ソートがクライアントサイドのみ（取得済みの 1 ページ内のみ並び替えられ、ページをまたいだ順序が正しくない） | 🟠 MEDIUM |
| 2 | 統計 API が存在せず、コレクションの全体像を把握できない | 🟡 LOW |

## タスク

### 17.1 サーバーサイドソート

#### 問題の詳細

`videoList.js` の `sortVideos()` は現在ページに表示されている動画のみをソートする。
ページサイズが 20 件で合計 60 件ある場合、"タイトル昇順" を選んでも
2・3 ページ目に正しい順序で表示されない。

#### 実装内容

**Domain 層:**
- `VideoSortOrder.cs` — ソート順 enum を追加
  ```
  CreatedAtDesc（デフォルト）, CreatedAtAsc, TitleAsc, TitleDesc,
  DurationDesc, FileSizeDesc
  ```
- `VideoSearchQuery.cs` — `SortOrder` プロパティを追加
- `IVideoRepository.cs` — `GetPagedAsync` / `SearchPagedAsync` の引数に `VideoSortOrder` を追加

**Application 層:**
- `SearchVideoRequest.cs` — `SortOrder` フィールドを追加（`VideoSortOrder` 型）
- `IListVideosUseCase.cs` — `sortOrder` パラメータを追加
- `ListVideosUseCase.cs` — ソート順をリポジトリに渡す
- `SearchVideosUseCase.cs` — `request.SortOrder` をクエリに渡す

**Infrastructure 層:**
- `VideoRepository.cs` — `GetPagedAsync` / `SearchPagedAsync` でソート順に応じた `OrderBy` を適用

**Functions 層:**
- `VideoFunctions.cs`
  - `ListVideos`: クエリパラメータ `sortBy` を追加（`createdAt`, `title`, `duration`, `fileSize`）
  - `ListVideos`: クエリパラメータ `sortDir` を追加（`asc`, `desc`）
  - `SearchVideos`: 同上

**Frontend:**
- `videoList.js` の `buildApiUrl()` に `sortBy` / `sortDir` パラメータを追加
- クライアントサイドの `sortVideos()` による並べ替えを削除し、サーバーから返った順序をそのまま表示

### 17.2 統計 API

#### 実装内容

**Domain 層:**
- `VideoStats.cs` — 集計結果を表すレコードを追加（`Domain/Repositories/` 配下）
  ```
  TotalCount, PendingCount, DownloadingCount, ProcessingCount,
  ReadyCount, FailedCount, TotalFileSizeBytes
  ```
- `IVideoRepository.cs` — `GetStatsAsync()` メソッドを追加

**Application 層:**
- `VideoStatsDto.cs` — クライアントへ返す DTO
- `IGetStatsUseCase.cs` — インターフェース定義
- `GetStatsUseCase.cs` — `IVideoRepository.GetStatsAsync()` を呼び出す実装
- `DependencyInjection.cs` — `GetStatsUseCase` を登録

**Infrastructure 層:**
- `VideoRepository.cs` — `GetStatsAsync()` を EF LINQ で実装
  （`GroupBy(v => v.Status).Count()` + `Sum(v => v.FileSizeBytes)`）

**Functions 層:**
- `VideoFunctions.cs` — `GET /api/stats` エンドポイントを追加

**Frontend:**
- `videoList.js` — ページロード時に `/api/stats` を取得
- `videoList.js` — ヘッダーにステータス別件数バーを表示
- `js/api.js` — `getStats()` ヘルパーを追加

## 完了条件

- [ ] `GET /api/videos?sortBy=title&sortDir=asc` が正しい順序で動画を返す
- [ ] `GET /api/videos/search?sortBy=duration&sortDir=desc` が正しい順序で動画を返す
- [ ] `VideoSortOrder` に未定義の文字列を渡した場合はデフォルト（`CreatedAtDesc`）が適用される
- [ ] `GET /api/stats` が `{ totalCount, pendingCount, downloadingCount, processingCount, readyCount, failedCount, totalFileSizeBytes }` を返す
- [ ] フロントエンドのソートがサーバーサイドに移行され、クライアントサイドの `sortVideos()` による並べ替えが削除される
- [ ] `dotnet test` で全 C# テストが通過する
- [ ] `npm test` で全 JS テストが通過する
- [ ] CLAUDE.md の規約違反がない
