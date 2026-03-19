# Sprint 3: Application UseCases

## 目的

アプリケーション層のユースケース、DTO、サービスインターフェースの実装。

## ブランチ

`feature/sprint3/application-usecases`

## タスク

### 3.1 DTO 定義

| DTO | 用途 |
|-----|------|
| `VideoDto` | 動画情報レスポンス |
| `VideoListItemDto` | 一覧表示用（軽量版） |
| `TagDto` | タグ情報 |
| `CategoryDto` | カテゴリ情報 |
| `RegisterVideoRequest` | 動画登録リクエスト |
| `UpdateVideoRequest` | 動画更新リクエスト |
| `SearchVideoRequest` | 検索リクエスト |
| `PaginatedResult<T>` | ページネーション結果 |

### 3.2 サービスインターフェース

| インターフェース | 説明 |
|----------------|------|
| `IVideoDownloadService` | 動画ダウンロード |
| `IBlobStorageService` | Blob ストレージ操作 |
| `IThumbnailService` | サムネイル生成・取得 |

### 3.3 ユースケース

| ユースケース | 説明 |
|------------|------|
| `RegisterVideoUseCase` | URL から動画を登録（ダウンロードキュー追加） |
| `GetVideoUseCase` | 動画詳細取得 |
| `ListVideosUseCase` | 動画一覧取得（ページネーション） |
| `SearchVideosUseCase` | 動画検索（タイトル、タグ、カテゴリ） |
| `UpdateVideoUseCase` | 動画情報更新（タイトル、タグ、カテゴリ） |
| `DeleteVideoUseCase` | 動画削除（Blob も削除） |
| `DownloadVideoUseCase` | 動画ダウンロード実行（バックグラウンド） |
| `ManageTagsUseCase` | タグ CRUD |
| `ManageCategoriesUseCase` | カテゴリ CRUD |

### 3.4 ユニットテスト

- 各ユースケースの正常系テスト
- 各ユースケースの異常系テスト（バリデーションエラー、存在しないリソース）
- リポジトリ・サービスの Mock 使用

## 完了条件

- [ ] すべての DTO が定義されている
- [ ] すべてのサービスインターフェースが定義されている
- [ ] すべてのユースケースが実装されている
- [ ] Application プロジェクトは Domain のみに依存
- [ ] ユニットテストがすべてパスする
