# Sprint 15: 技術的負債解消

## 目的

Sprint 14 までに蓄積された技術的負債を解消し、コードの品質・安全性・テストカバレッジを向上させる。

## ブランチ

`feature/sprint15/tech-debt`

## 背景・問題点

| # | 問題 | 深刻度 |
|---|------|--------|
| 1 | `VideoFunctions.cs` の fire-and-forget（`Task.Run` 放置） | 🔴 Critical — CLAUDE.md 明示禁止 |
| 2 | 全 Repository が `SaveChangesAsync()` を直接呼び出し、UnitOfWork を迂回 | 🟠 High — アーキテクチャ違反 |
| 3 | フロントエンドテストが存在しない（Vitest 設定済みだがテストファイルなし） | 🟡 Medium — テストギャップ |

## タスク

### 15.1 Queue Trigger による非同期ダウンロード

CLAUDE.md のルール: **「Azure Functions Consumption Plan では fire-and-forget（`Task.Run` 放置）を禁止する。非同期処理は Queue Trigger 等のメッセージング経由で実行する」**

#### 実装内容

- `Application/Services/IDownloadQueueService.cs` — キュー送信インターフェース（Application 層）
- `Infrastructure/Services/StorageQueueDownloadQueueService.cs` — Azure Storage Queue 実装
- `Infrastructure/Options/QueueStorageOptions.cs` — 接続設定
- `Functions/Functions/DownloadVideoQueueFunction.cs` — Queue Trigger 関数
- `VideoFunctions.cs` の `RegisterVideoAsync` を修正:
  - `Task.Run()` + `ContinueWith()` を削除
  - `IDownloadQueueService.EnqueueAsync(videoId)` に置き換え

#### 追加パッケージ

- Infrastructure: `Azure.Storage.Queues`
- Functions: `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues`

### 15.2 Repository の UnitOfWork 修正

各 Repository が `SaveChangesAsync()` を個別呼び出しすると、複数リポジトリをまたぐ操作（例: VideoTag 削除 → Video 削除）がトランザクション化されない。

#### 実装内容

**Repository 修正（`SaveChangesAsync` を削除）:**
- `VideoRepository` — `AddAsync`, `UpdateAsync`, `DeleteAsync`
- `TagRepository` — `AddAsync`, `UpdateAsync`, `DeleteAsync`
- `CategoryRepository` — `AddAsync`, `UpdateAsync`, `DeleteAsync`
- `VideoTagRepository` — `AddAsync`, `DeleteAsync`, `DeleteByVideoIdAsync`, `SyncByVideoIdAsync`

**UseCase 修正（`IUnitOfWork` を注入し `SaveChangesAsync` を呼び出す）:**
- `RegisterVideoUseCase`
- `UpdateVideoUseCase`
- `DeleteVideoUseCase`
- `DownloadVideoUseCase`
- `ManageTagsUseCase`
- `ManageCategoriesUseCase`

**テスト更新:**
- Application テスト: `IUnitOfWork` モックを追加
- Infrastructure テスト: `_db.SaveChangesAsync()` を明示的に呼び出すよう修正

### 15.3 フロントエンドテスト追加

テストファイルは `tests/js/` 配下に配置する（`vitest.config.js` の設定に従う）。

#### テスト対象

| ファイル | テスト内容 |
|---------|-----------|
| `tests/js/utils/format.test.js` | `formatDate`, `formatFileSize`, `formatDuration` |
| `tests/js/utils/dom.test.js` | `createElement`, `clearChildren` |
| `tests/js/utils/debounce.test.js` | デバウンス動作 |
| `tests/js/utils/tagColor.test.js` | タグ色マッピング |
| `tests/js/router.test.js` | ルーティング、パラメータ解析 |

## 完了条件

- [x] `VideoFunctions.cs` で fire-and-forget が解消され、Queue 経由でダウンロードが実行される
- [x] 全 Repository が `SaveChangesAsync()` を呼ばず、UseCase が `IUnitOfWork` 経由でコミットする
- [x] `tests/js/` 配下に Vitest テストが実装され `npm test` で全件通過する
- [x] `dotnet test` で全 C# テストが通過する
- [x] CLAUDE.md の規約違反がない
