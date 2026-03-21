# 改善提案書

**作成日:** 2026-03-21
**対象:** X Video Collector プロジェクト（Sprint 14 完了後）
**前提:** [コードレビュー報告書](code-review.md) の指摘事項を踏まえた提案

---

## 概要

Sprint 0〜14 で基盤機能は概ね完成。本提案書では、コードレビューで識別された技術的負債の解消、セキュリティ強化、運用品質の向上、および将来の機能拡張を、優先度・工数の観点から次期スプリントとして整理する。

---

## Sprint 15: アーキテクチャ改善（技術的負債解消）

**目的:** コードレビューの CRITICAL / HIGH 指摘事項を解消し、本番運用に耐える品質に引き上げる

### 15.1 Queue Trigger による動画ダウンロード非同期化

**優先度:** CRITICAL | **工数目安:** 大

**現状の問題:**
- `VideoFunctions.RegisterVideo` 内で `Task.Run` による fire-and-forget でダウンロード処理を実行
- Azure Functions Consumption Plan ではホストプロセス回収によりダウンロードが中断される可能性
- 例外が握りつぶされ、失敗を検知できない

**改善内容:**
1. Azure Storage Queue を追加（`download-requests` キュー）
2. `RegisterVideo` はキューにメッセージ（VideoId）を発行するだけに変更
3. 新しい `DownloadVideoQueueFunction` を Queue Trigger として実装
4. リトライポリシーを `host.json` で設定（`maxDequeueCount: 3`）
5. Poison Queue での失敗通知メカニズム追加

**影響範囲:**
- `VideoFunctions.cs` — `Task.Run` 削除、Queue メッセージ発行追加
- 新規 `DownloadVideoQueueFunction.cs` 作成
- `host.json` — Queue 設定追加
- `infra/modules/storage.bicep` — Queue リソース追加
- フロントエンド — 登録後のポーリングまたは状態表示改善

### 15.2 IUnitOfWork パターンの実活用

**優先度:** CRITICAL | **工数目安:** 中

**現状の問題:**
- `IUnitOfWork` は定義・DI 登録済みだが、どの UseCase からも呼び出されていない
- 全 Repository が `SaveChangesAsync` を内部で個別に呼び出している
- 複数リポジトリにまたがる操作（Update, Delete）がトランザクション保護されない

**改善内容:**
1. 全 Repository から `SaveChangesAsync` 呼び出しを削除
2. 各 UseCase に `IUnitOfWork` を注入し、末尾で `await unitOfWork.SaveChangesAsync()` を呼出
3. `DownloadVideoUseCase` は中間状態保存が必要なため、明示的に `SaveChangesAsync` を挟む設計を文書化
4. テストを更新して `IUnitOfWork.SaveChangesAsync` の呼び出しを検証

**影響範囲:**
- 全 Repository 実装（4ファイル）
- 全 UseCase 実装（9ファイル）
- 全 UseCase テスト

### 15.3 シークレット管理の Key Vault 移行

**優先度:** CRITICAL | **工数目安:** 中

**現状の問題:**
- SQL 接続文字列（パスワード含む）が Bicep 出力 → Functions App 設定に平文で保存
- Storage 接続文字列も同様
- ARM デプロイ履歴にシークレットが記録される

**改善内容:**
1. Azure Key Vault リソースを Bicep に追加
2. SQL パスワード、Storage 接続文字列を Key Vault Secret として格納
3. Functions App 設定で Key Vault 参照 (`@Microsoft.KeyVault(SecretUri=...)`) を使用
4. Functions App に Key Vault への Managed Identity アクセスを設定
5. Bicep 出力からシークレットを含む値を削除

**影響範囲:**
- `infra/main.bicep` — Key Vault モジュール追加
- 新規 `infra/modules/keyvault.bicep`
- `infra/modules/functions.bicep` — Key Vault 参照に変更
- `infra/modules/sql.bicep` — 出力からパスワード削除

---

## Sprint 16: セキュリティ強化

**目的:** 認証・認可の防御層を追加し、攻撃面を最小化する

### 16.1 Functions 直接アクセス防御

**優先度:** HIGH | **工数目安:** 小

**改善内容:**
1. ミドルウェアで `X-MS-CLIENT-PRINCIPAL` ヘッダーを検証
2. ヘッダーがない場合は 401 Unauthorized を返す
3. 開発環境（ローカル）では検証をスキップするオプション追加

### 16.2 入力バリデーション強化

**優先度:** MEDIUM | **工数目安:** 小

**改善内容:**
1. `pageSize` の上限設定（最大 100）
2. Tag / Category の Name に最大長チェック追加（100文字）
3. Category の Name に一意制約追加（DB レベル）
4. VideoTag に外部キー制約追加

### 16.3 例外処理の改善

**優先度:** MEDIUM | **工数目安:** 小

**改善内容:**
1. `NotFoundException` カスタム例外クラスの作成
2. UseCase で `InvalidOperationException` の代わりに `NotFoundException` を使用
3. `ExceptionMiddleware` で文字列マッチングではなく型ベースのパターンマッチに変更

---

## Sprint 17: テスト品質向上

**目的:** テストカバレッジを拡充し、品質を保証する仕組みを強化する

### 17.1 不足テストの追加

**優先度:** HIGH | **工数目安:** 中

**追加すべきテスト:**

| テスト対象 | テスト内容 |
|-----------|-----------|
| Category エンティティ | Create, Update のバリデーション・状態変更テスト |
| GetVideoUseCase | 正常系、Video 未検出時、タグ付き取得 |
| DownloadVideoUseCase | 正常系（状態遷移）、ダウンロード失敗、タイムアウト |
| BlobStorageService | Upload, Download, SAS URL 生成、Delete |
| TagRepository | CRUD 操作、GetByVideoIdsAsync のバッチ取得 |
| VideoTagRepository | Sync 操作、Delete 操作 |
| UpdateVideoUseCase | 空タイトル、無効タグ ID、タグ全削除 |
| DeleteVideoUseCase | BlobPath ありの動画削除時の Blob クリーンアップ |
| UpdatedAt 監査プロパティ | 状態変更時の UpdatedAt 更新検証 |

### 17.2 統合テスト基盤の整備

**優先度:** MEDIUM | **工数目安:** 中

**改善内容:**
1. `WebApplicationFactory` を使った Azure Functions の統合テスト基盤
2. EF Core の InMemoryDatabase から SQLite In-Memory への移行（SQL 互換性向上）
3. リポジトリの SearchPagedAsync / GetPagedAsync のページネーション統合テスト

### 17.3 フロントエンドテスト改善

**優先度:** LOW | **工数目安:** 小

**改善内容:**
1. `api.test.js` の `global.fetch` モック afterEach クリーンアップ追加
2. AbortController を使ったリクエストキャンセルテスト
3. router.test.js の拡充（パラメータ付きルート、404 処理）

---

## Sprint 18: インフラ・CI/CD 改善

**目的:** デプロイの信頼性と運用品質を向上させる

### 18.1 Bicep の修正

**優先度:** HIGH | **工数目安:** 小

**改善内容:**
1. `functions.bicep` の `netFrameworkVersion` を `v10.0` に修正
2. NuGet パッケージのワイルドカードバージョンをピン留め
3. `appinsights.bicep` の未使用 `instrumentationKey` 出力を削除
4. Static Web Apps の `__TENANT_ID__` をデプロイパイプラインで自動置換

### 18.2 CI パイプライン修正

**優先度:** HIGH | **工数目安:** 小

**改善内容:**
1. `build-functions` ジョブに `dotnet restore` ステップを追加（`--no-restore` 問題の解消）
2. ソリューションレベルでの restore/build に簡素化
3. Deploy ワークフローに CI 成功の前提条件を追加（`workflow_run` またはブランチ保護ルール）
4. `host.json` の `Host.Aggregator` ログレベルを `Information` に変更

### 18.3 Managed Identity 移行

**優先度:** MEDIUM | **工数目安:** 中

**改善内容:**
1. Functions App に System-Assigned Managed Identity を有効化
2. Blob Storage への Managed Identity + RBAC アクセスに切り替え
3. SQL Database への Managed Identity 認証に切り替え（接続文字列からパスワード排除）
4. `BlobStorageService` を `DefaultAzureCredential` ベースに変更

---

## Sprint 19: 機能改善・UX 向上

**目的:** ユーザー体験を向上させる機能改善

### 19.1 動画再生時間の取得

**優先度:** MEDIUM | **工数目安:** 小

**現状:** `DurationSeconds` が常に 0 で、フロントエンドに "00:00" と表示される

**改善内容:**
1. ダウンロード後に ffprobe で動画の再生時間を取得
2. `YtDlpDownloadService` の `DownloadResult` に正しい `DurationSeconds` を設定
3. フロントエンドの `formatDuration` で 0 の場合は「不明」と表示

### 19.2 コンテンツタイプの動的判定

**優先度:** LOW | **工数目安:** 小

**改善内容:**
1. ダウンロードされたファイルの拡張子からコンテンツタイプを判定
2. `BlobStorageService.UploadVideoAsync` にコンテンツタイプパラメータを追加

### 19.3 フロントエンド改善

**優先度:** MEDIUM | **工数目安:** 中

**改善内容:**
1. AbortController によるリクエストキャンセル（レースコンディション防止）
2. フィルタ変更のデバウンス（連続 API 呼び出し防止）
3. `confirm()` をカスタムモーダルに置き換え（UX 統一）
4. 動画詳細ページのモーダルにフォーカストラップ追加（アクセシビリティ）
5. tagIds/tags クエリパラメータ名の統一

### 19.4 JSON シリアライズ設定の一元管理

**優先度:** LOW | **工数目安:** 小

**改善内容:**
1. `FunctionHelper.JsonOptions` と `Program.cs` の `JsonOptions` を統一
2. 共通の `JsonSerializerOptions` ファクトリを作成

---

## Sprint 20: 運用・監視

**目的:** 本番環境での安定運用を実現する

### 20.1 ヘルスチェック API

**優先度:** MEDIUM | **工数目安:** 小

**改善内容:**
1. `/api/health` エンドポイント追加
2. SQL Database、Blob Storage への接続チェック
3. yt-dlp / ffmpeg バイナリの存在チェック

### 20.2 Application Insights 活用

**優先度:** MEDIUM | **工数目安:** 小

**改善内容:**
1. カスタムメトリクス追加（ダウンロード成功/失敗率、処理時間）
2. アラートルール設定（エラー率閾値、ダウンロード失敗連続発生）
3. ダッシュボード作成

### 20.3 データベースマイグレーション自動化

**優先度:** LOW | **工数目安:** 中

**改善内容:**
1. EF Core Migrations の導入
2. デプロイパイプラインでのマイグレーション自動実行
3. ロールバック手順の整備

---

## 将来的な拡張案（優先度未定）

以下は現時点では優先度が低いが、将来的に検討する価値がある機能拡張。

### 一括操作

- 複数動画の一括タグ付け・カテゴリ変更
- 一括削除機能
- CSV/JSON によるメタデータのインポート・エクスポート

### 高度な検索

- 全文検索（Azure Cognitive Search 連携）
- 日付範囲フィルタ
- ファイルサイズ・再生時間によるフィルタ
- お気に入り / ブックマーク機能

### メディア管理

- 動画のプレビューサムネイル自動生成（タイムライン形式）
- 動画の圧縮・トランスコード（ストレージ節約）
- 重複動画検出

### ソーシャル連携

- X API との統合（ツイートメタデータの自動取得）
- 定期的なフィード監視・自動収集（Timer Trigger）

### 運用改善

- マルチテナント対応
- バックアップ・リストア機能
- 使用量ダッシュボード（ストレージ容量、API 呼び出し数）

---

## スプリント優先度まとめ

| Sprint | テーマ | 優先度 | 主な改善内容 |
|--------|--------|--------|-------------|
| 15 | アーキテクチャ改善 | 最高 | Queue Trigger 化、UnitOfWork 活用、Key Vault |
| 16 | セキュリティ強化 | 高 | Functions 認証防御、入力バリデーション、例外処理 |
| 17 | テスト品質向上 | 高 | 不足テスト追加、統合テスト基盤、FE テスト改善 |
| 18 | インフラ・CI/CD | 高 | Bicep 修正、CI 修正、Managed Identity |
| 19 | 機能改善・UX | 中 | DurationSeconds、AbortController、モーダル改善 |
| 20 | 運用・監視 | 中 | ヘルスチェック、メトリクス、DB マイグレーション |

Sprint 15〜16 は本番リリース前に必ず対応すべき項目。Sprint 17〜18 はリリース後早期に対応が望ましい。Sprint 19〜20 は安定運用開始後に順次実施。
