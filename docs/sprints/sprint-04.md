# Sprint 4: Repositories & Blob Storage

## 目的

Infrastructure 層のリポジトリ実装（EF Core + Azure SQL）と Blob ストレージサービス実装。

## ブランチ

`feature/sprint4/repositories-blob`

## タスク

### 4.1 EF Core DbContext

- `AppDbContext` 作成
- エンティティ構成（Fluent API）
- Video, Tag, Category, VideoTag のマッピング

### 4.2 リポジトリ実装

| クラス | インターフェース |
|-------|----------------|
| `VideoRepository` | `IVideoRepository` |
| `TagRepository` | `ITagRepository` |
| `CategoryRepository` | `ICategoryRepository` |

### 4.3 Blob ストレージサービス

- `BlobStorageService` : `IBlobStorageService`
- 動画ファイルアップロード
- サムネイルアップロード
- SAS トークン付き URL 生成（ストリーミング用）
- ファイル削除

### 4.4 DI 登録

- `DependencyInjection.cs` に全サービスの登録
- Options パターンで設定バインド

### 4.5 EF Core マイグレーション

- 初回マイグレーション作成
- マイグレーション適用スクリプト

### 4.6 統合テスト

- InMemory プロバイダーを使用したリポジトリテスト
- Blob ストレージの Mock テスト

## 完了条件

- [ ] DbContext が正しく構成されている
- [ ] すべてのリポジトリが実装されている
- [ ] Blob ストレージサービスが実装されている
- [ ] DI 登録が完了している
- [ ] マイグレーションが作成されている
- [ ] テストがすべてパスする
