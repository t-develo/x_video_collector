# Sprint 2: Domain Entities

## 目的

ドメインモデル（エンティティ、値オブジェクト、列挙型、リポジトリインターフェース）の実装。

## ブランチ

`feature/sprint2/domain-entities`

## タスク

### 2.1 値オブジェクト

| 値オブジェクト | 説明 |
|--------------|------|
| `TweetUrl` | X ポスト URL（正規化・バリデーション付き） |
| `VideoTitle` | 動画タイトル（最大200文字） |
| `BlobPath` | Blob ストレージ内パス |

### 2.2 列挙型

| 列挙型 | 値 |
|-------|-----|
| `VideoStatus` | `Pending`, `Downloading`, `Processing`, `Ready`, `Failed` |
| `TagColor` | 色定義（UI 表示用） |

### 2.3 エンティティ

| エンティティ | 主要プロパティ |
|------------|--------------|
| `Video` | Id, TweetUrl, Title, Status, BlobPath, ThumbnailBlobPath, DurationSeconds, FileSizeBytes, CreatedAt, UpdatedAt |
| `Tag` | Id, Name, Color, CreatedAt |
| `Category` | Id, Name, SortOrder, CreatedAt |
| `VideoTag` | VideoId, TagId（多対多中間） |

### 2.4 リポジトリインターフェース

| インターフェース | メソッド |
|----------------|---------|
| `IVideoRepository` | GetById, GetAll, Search, Add, Update, Delete |
| `ITagRepository` | GetById, GetAll, GetByVideoId, Add, Update, Delete |
| `ICategoryRepository` | GetById, GetAll, Add, Update, Delete |

### 2.5 ユニットテスト

- 値オブジェクトのバリデーションテスト
- エンティティのファクトリメソッドテスト
- エンティティの状態遷移テスト

## 完了条件

- [ ] すべての値オブジェクトが実装されている
- [ ] すべての列挙型が定義されている
- [ ] すべてのエンティティが実装されている
- [ ] リポジトリインターフェースが定義されている
- [ ] ドメインプロジェクトに外部パッケージ参照がない
- [ ] ユニットテストがすべてパスする
