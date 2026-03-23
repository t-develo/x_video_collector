# Sprint 20: 運用・監視

## 目的

本番環境での安定運用を実現する。ヘルスチェック API、Application Insights カスタムメトリクス、EF Core マイグレーション自動化を導入する。

## ブランチ

`claude/sprint-20-start-iFEZX`

## タスク

### 20.1 ヘルスチェック API

`/api/health` エンドポイントを追加し、以下の接続状態を確認する。

| チェック対象 | 確認内容 |
|-------------|---------|
| `sql` | `AppDbContext.Database.CanConnectAsync()` による SQL 接続確認 |
| `blob` | `BlobServiceClient.GetPropertiesAsync()` による Blob Storage 接続確認 |
| `ytdlp` | `YtDlp.ExecutablePath` のファイル存在確認 |
| `ffmpeg` | `YtDlp.FfmpegPath` のファイル存在確認 |
| `ffprobe` | `YtDlp.FfprobePath` のファイル存在確認 |

**レスポンス例（正常）:**

```json
{
  "status": "Healthy",
  "checks": {
    "sql": { "status": "Healthy", "durationMs": 12 },
    "blob": { "status": "Healthy", "durationMs": 8 },
    "ytdlp": { "status": "Healthy", "message": "D:\\home\\site\\wwwroot\\yt-dlp.exe", "durationMs": 1 },
    "ffmpeg": { "status": "Healthy", "message": "D:\\home\\site\\wwwroot\\ffmpeg.exe", "durationMs": 1 },
    "ffprobe": { "status": "Healthy", "message": "D:\\home\\site\\wwwroot\\ffprobe.exe", "durationMs": 1 }
  },
  "timestamp": "2026-03-23T12:00:00+00:00"
}
```

**HTTP ステータス:** 全て Healthy → 200、いずれか Unhealthy → 503

**認証:** `/api/health` は `AuthMiddleware` をスキップし、認証なしでアクセス可能。

### 20.2 Application Insights カスタムメトリクス

#### カスタムイベント

| イベント名 | 計測タイミング | プロパティ | メトリクス |
|-----------|--------------|-----------|-----------|
| `VideoDownloadSuccess` | ダウンロード完了時 | `VideoId`, `FileSizeBytes` | `DurationSeconds`, `FileSizeMB` |
| `VideoDownloadFailure` | ダウンロード失敗時 | `VideoId`, `FailureReason` | `DurationSeconds` |

#### カスタムメトリクス

- `VideoDownload.DurationSeconds` — `Outcome` ディメンション（`Success` / `Failure`）でスライス可能

#### アラートルール（Bicep）

| アラート名 | 条件 | 重要度 |
|-----------|------|--------|
| `alert-download-failure` | 5分間でダウンロード失敗 3件以上 | Sev2 |
| `alert-server-errors` | 5分間のサーバーエラー率 5%以上 | Sev1 |

アラートのメール通知を有効にするには Bicep デプロイ時に `alertEmailAddress` を指定する。

```bash
az deployment sub create \
  --template-file infra/main.bicep \
  --parameters infra/parameters.json \
               alertEmailAddress="your-email@example.com" \
               sqlAdminPassword="..."
```

### 20.3 EF Core マイグレーション自動化

#### 初回マイグレーション

`InitialCreate` マイグレーションを生成済み（`src/api/XVideoCollector.Infrastructure/Persistence/Migrations/`）。

#### 自動実行（CI/CD）

`deploy.yml` に `migrate-database` ジョブを追加。`infra` ジョブ完了後、`deploy-functions` より前に実行される。

```yaml
jobs:
  infra → migrate-database → deploy-functions
                           → deploy-frontend
```

`SQL_CONNECTION_STRING` シークレットを GitHub リポジトリに設定する必要がある。

#### 手動実行

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet tool install --global dotnet-ef

dotnet ef database update \
  --project src/api/XVideoCollector.Infrastructure \
  --connection "Server=...;Database=XVideoCollector;..."
```

#### ロールバック手順

**1. 直前のマイグレーションへのロールバック:**

```bash
# 適用済みマイグレーション一覧を確認
dotnet ef migrations list \
  --project src/api/XVideoCollector.Infrastructure \
  --connection "Server=...;..."

# 1つ前のマイグレーションへロールバック（<PreviousMigrationName> は target）
dotnet ef database update <PreviousMigrationName> \
  --project src/api/XVideoCollector.Infrastructure \
  --connection "Server=...;..."
```

**2. 初期状態へのロールバック（全テーブル削除）:**

```bash
dotnet ef database update 0 \
  --project src/api/XVideoCollector.Infrastructure \
  --connection "Server=...;..."
```

> ⚠️ **注意:** ロールバック前に必ずデータベースのバックアップを取得すること。
> Azure SQL Database では Azure Portal → データベース → バックアップ → 今すぐバックアップ。

**3. マイグレーション履歴の確認:**

```sql
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
```

**4. 緊急時の手動ロールバック SQL:**

`Down()` メソッドの内容を手動で SQL として実行する。
各マイグレーションファイル（`...cs`）の `Down()` メソッドを参照すること。

## 完了条件

- [x] `GET /api/health` が 200 または 503 を返す
- [x] SQL/Blob/バイナリの各チェック結果が JSON に含まれる
- [x] 認証なしで `/api/health` にアクセスできる
- [x] `VideoDownloadSuccess` / `VideoDownloadFailure` カスタムイベントが AI に送信される
- [x] ダウンロード成功/失敗時に `VideoDownload.DurationSeconds` メトリクスが記録される
- [x] Bicep にアラートルール 2件が定義される
- [x] EF Core 初期マイグレーションが生成される
- [x] `deploy.yml` でマイグレーション自動実行ジョブが追加される
- [x] ロールバック手順が本ドキュメントに記載される
- [x] `dotnet test` で全 C# テストが通過する
