# X Video Collector

X（旧 Twitter）の動画を個人的に収集・保存・管理するための Web アプリケーション。
Azure サーバーレス構成で無料枠内での運用を目指す。

## 機能概要

- X（旧 Twitter）のツイート URL を入力して動画を登録
- yt-dlp + ffmpeg による自動動画ダウンロード・サムネイル生成
- タグ・カテゴリによる動画の分類と管理
- キーワード検索、ステータス/タグ/カテゴリによるフィルタリング
- ページネーション付き動画一覧表示
- SAS URL を利用したセキュアな動画ストリーミング再生
- Azure Entra ID（旧 Azure AD）によるシングルサインオン認証
- レスポンシブ対応の Industrial Minimal ダークテーマ UI

## 技術スタック

| レイヤー | 技術 |
|---------|------|
| バックエンド | C# .NET 10, Azure Functions (Isolated Worker, Windows Consumption) |
| フロントエンド | Vanilla JS (ES2022+), HTML5, CSS3 — フレームワーク不使用 SPA |
| データベース | Azure SQL Database (Free Tier) |
| ストレージ | Azure Blob Storage |
| 認証 | Azure Static Web Apps 組み込み Entra ID 認証 |
| 動画ダウンロード | yt-dlp + ffmpeg（Process.Start で直接呼び出し） |
| テスト | xUnit + Moq (C#), Vitest + jsdom (JS) |
| CI/CD | GitHub Actions |
| IaC | Bicep |

## アーキテクチャ

### クリーンアーキテクチャ（4層構造）

```
Domain（中心）→ Application → Infrastructure → Functions（最外層）
```

依存は常に外→内の一方向のみ。Domain 層は外部パッケージ依存ゼロ。

### インフラ構成

```
┌─────────────────────────────────────────────────────────┐
│                    Azure Subscription                    │
│                                                         │
│  ┌──────────────────┐    ┌───────────────────────────┐  │
│  │  Static Web Apps  │───▶│   Azure Functions         │  │
│  │  (Frontend SPA)   │    │   (Isolated Worker/.NET10)│  │
│  │                   │    │                           │  │
│  │  - Entra ID 認証  │    │   - Video CRUD API        │  │
│  │  - SPA ルーティング│    │   - Tag / Category API    │  │
│  │  - CDN 配信       │    │   - yt-dlp ダウンロード    │  │
│  └──────────────────┘    └───────────┬───────────────┘  │
│                                      │                   │
│                          ┌───────────┴───────────┐      │
│                          │                       │      │
│                  ┌───────▼──────┐  ┌─────────────▼──┐   │
│                  │  Azure SQL    │  │  Blob Storage   │   │
│                  │  Database     │  │                 │   │
│                  │  (Free Tier)  │  │  - videos/      │   │
│                  │              │  │  - thumbnails/   │   │
│                  └──────────────┘  └─────────────────┘   │
│                                                         │
│  ┌──────────────────┐                                   │
│  │ Application       │                                   │
│  │ Insights          │  ← テレメトリ・ログ収集            │
│  └──────────────────┘                                   │
└─────────────────────────────────────────────────────────┘
```

### ソリューション構造

```
XVideoCollector.sln
├── src/
│   ├── api/
│   │   ├── XVideoCollector.Domain/         # エンティティ, 値オブジェクト, リポジトリI/F
│   │   ├── XVideoCollector.Application/    # ユースケース, DTO, サービスI/F
│   │   ├── XVideoCollector.Infrastructure/ # EF Core, Blob Storage, yt-dlp 実装
│   │   └── XVideoCollector.Functions/      # Azure Functions エンドポイント
│   └── frontend/
│       ├── index.html                      # SPA エントリーポイント
│       ├── css/                            # CSS カスタムプロパティによるテーマ管理
│       ├── js/                             # ES Modules ベースの SPA コード
│       └── staticwebapp.config.json        # SWA ルーティング・認証設定
├── tests/
│   ├── XVideoCollector.Domain.Tests/
│   ├── XVideoCollector.Application.Tests/
│   ├── XVideoCollector.Infrastructure.Tests/
│   ├── XVideoCollector.Functions.Tests/
│   └── js/                                 # フロントエンドテスト（Vitest）
├── infra/
│   ├── main.bicep                          # サブスクリプションスコープ IaC
│   └── modules/                            # リソースモジュール（SQL, Storage, etc.）
├── scripts/
│   └── setup.sh                            # 初期セットアップ自動化スクリプト
└── docs/
    ├── code-review.md                      # コードレビュー報告書
    ├── deployment.md                       # デプロイ手順書
    ├── improvement-proposal.md             # 改善提案書
    └── sprints/                            # スプリント計画書（Sprint 0〜14）
```

## API エンドポイント

### 動画 API

| メソッド | パス | 説明 |
|---------|------|------|
| POST | `/api/videos` | 動画登録（ツイート URL + タイトル） |
| GET | `/api/videos` | 動画一覧取得（ページネーション対応） |
| GET | `/api/videos/{id}` | 動画詳細取得 |
| PUT | `/api/videos/{id}` | 動画更新（タイトル, カテゴリ, タグ） |
| DELETE | `/api/videos/{id}` | 動画削除（Blob 含む） |
| GET | `/api/videos/{id}/stream` | ストリーミング URL 取得（SAS URL） |
| GET | `/api/videos/search` | 動画検索（キーワード, ステータス, タグ, カテゴリ） |

### タグ API

| メソッド | パス | 説明 |
|---------|------|------|
| GET | `/api/tags` | 全タグ取得 |
| POST | `/api/tags` | タグ作成 |
| PUT | `/api/tags/{id}` | タグ更新 |
| DELETE | `/api/tags/{id}` | タグ削除 |

### カテゴリ API

| メソッド | パス | 説明 |
|---------|------|------|
| GET | `/api/categories` | 全カテゴリ取得 |
| POST | `/api/categories` | カテゴリ作成 |
| PUT | `/api/categories/{id}` | カテゴリ更新 |
| DELETE | `/api/categories/{id}` | カテゴリ削除 |

## セットアップ

### 前提条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 18 以上
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [GitHub CLI](https://cli.github.com/)
- Azure サブスクリプション
- `jq`, `curl`, `unzip`

### クイックスタート（初回デプロイ）

`scripts/setup.sh` で Azure リソースの初期セットアップからデプロイまでを自動化できます。

```bash
# 前提条件のインストール確認後に実行
bash scripts/setup.sh
```

スクリプトが実行する内容:

1. Entra ID アプリ登録（SWA 認証用）
2. GitHub OIDC サービスプリンシパル + Federated Credentials 設定
3. GitHub Secrets 設定（`AZURE_*` + `SQL_ADMIN_PASSWORD`）
4. `staticwebapp.config.json` にテナント ID を自動設定
5. yt-dlp.exe / ffmpeg.exe ダウンロード（Windows 実行ファイル）
6. 初回 Bicep インフラデプロイ
7. Entra ID リダイレクト URI 登録

スクリプトのオプション:

| オプション | 説明 | デフォルト |
|-----------|------|-----------|
| `--repo` | GitHub リポジトリ（`owner/repo` 形式） | git remote から自動取得 |
| `--location` | Azure リージョン | `japaneast` |
| `--skip-deploy` | Bicep デプロイをスキップ | — |

スクリプト完了後:

```bash
git add src/frontend/staticwebapp.config.json
git commit -m "chore: set tenant ID in SWA config"
git push origin main  # → Deploy ワークフローが自動実行
```

詳細な手動セットアップ手順は [docs/deployment.md](docs/deployment.md) を参照してください。

### ローカル開発

```bash
# 1. リポジトリをクローン
git clone https://github.com/t-develo/x_video_collector.git
cd x_video_collector

# 2. .NET の依存関係を復元
dotnet restore

# 3. JS の依存関係をインストール（テスト用）
npm install

# 4. バックエンドのビルド
dotnet build

# 5. テストの実行
dotnet test                # C# テスト（151件）
npx vitest run             # JS テスト（188件）

# 6. Azure Functions のローカル実行（Azure Functions Core Tools が必要）
cd src/api/XVideoCollector.Functions
func start
```

ローカル実行時の設定（`local.settings.json`）:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ConnectionStrings__SqlDb": "Server=(localdb)\\mssqllocaldb;Database=XVideoCollector;Trusted_Connection=True;",
    "BlobStorage__ConnectionString": "UseDevelopmentStorage=true",
    "BlobStorage__VideoContainerName": "videos",
    "BlobStorage__ThumbnailContainerName": "thumbnails",
    "YtDlp__ExecutablePath": "./yt-dlp.exe",
    "YtDlp__FfmpegPath": "./ffmpeg.exe",
    "YtDlp__TimeoutSeconds": "300",
    "YtDlp__MaxFileSizeMB": "500"
  }
}
```

## CI/CD

### CI（自動テスト・ビルド）

トリガー: `feature/**`, `claude/**` ブランチへの push、`main` への PR

| ジョブ | 内容 |
|--------|------|
| dotnet-test | .NET ビルド + 全テスト |
| js-test | Vitest フロントエンドテスト |
| build-functions | Azure Functions Publish（アーティファクト保存） |

### Deploy（自動デプロイ）

トリガー: `main` ブランチへの push

| ジョブ | 内容 |
|--------|------|
| infra | Bicep インフラデプロイ |
| deploy-functions | Azure Functions デプロイ |
| deploy-frontend | Static Web Apps デプロイ |

## 環境変数

### Azure Functions アプリケーション設定

| 変数名 | 説明 |
|--------|------|
| `ConnectionStrings__SqlDb` | Azure SQL 接続文字列 |
| `BlobStorage__ConnectionString` | Blob Storage 接続文字列 |
| `BlobStorage__VideoContainerName` | 動画コンテナ名 |
| `BlobStorage__ThumbnailContainerName` | サムネイルコンテナ名 |
| `YtDlp__ExecutablePath` | yt-dlp 実行ファイルパス |
| `YtDlp__FfmpegPath` | ffmpeg 実行ファイルパス |
| `YtDlp__TimeoutSeconds` | ダウンロードタイムアウト秒数 |
| `YtDlp__MaxFileSizeMB` | 最大ファイルサイズ (MB) |

### GitHub Secrets

| シークレット名 | 説明 |
|---------------|------|
| `AZURE_CLIENT_ID` | GitHub Actions 用サービスプリンシパル Client ID |
| `AZURE_TENANT_ID` | Entra ID テナント ID |
| `AZURE_SUBSCRIPTION_ID` | Azure サブスクリプション ID |
| `SQL_ADMIN_PASSWORD` | SQL Server 管理者パスワード |

## ドキュメント

| ドキュメント | 内容 | 関連 |
|-------------|------|------|
| [CLAUDE.md](CLAUDE.md) | プロジェクトルール・コーディング規約 | — |
| [docs/code-review.md](docs/code-review.md) | コードベースレビュー報告書（CRITICAL 3 / HIGH 5 / MEDIUM 8 / LOW 7） | → [改善提案書](docs/improvement-proposal.md) |
| [docs/improvement-proposal.md](docs/improvement-proposal.md) | 改善提案書（Sprint 15〜20） | ← [レビュー報告書](docs/code-review.md) |
| [docs/deployment.md](docs/deployment.md) | デプロイ手順書 | — |
| [docs/sprints/](docs/sprints/) | Sprint 0〜14 計画書 | — |

## ライセンス

Private — 個人利用
