# デプロイ手順書

## 前提条件

- Azure サブスクリプション
- Azure CLI (`az`) がインストール済み
- GitHub CLI (`gh`) がインストール済み
- .NET 10 SDK がインストール済み

---

## 1. Azure リソース初期セットアップ

### 1.1 Entra ID アプリ登録（認証用）

Static Web Apps の組み込み認証で使用する Entra ID アプリを登録します。

```bash
# テナント ID を確認
az account show --query tenantId -o tsv

# Entra ID アプリ登録
az ad app create \
  --display-name "X Video Collector" \
  --sign-in-audience AzureADMyOrg
```

取得した `appId` と テナント ID を `staticwebapp.config.json` の `openIdIssuer` に設定します。

```bash
TENANT_ID=$(az account show --query tenantId -o tsv)

# src/frontend/staticwebapp.config.json の __TENANT_ID__ を実際のテナント ID に置換
sed -i "s/__TENANT_ID__/${TENANT_ID}/g" src/frontend/staticwebapp.config.json
```

> **重要:** `openIdIssuer` には `/common` ではなくテナント固有の URL (`https://login.microsoftonline.com/{TENANT_ID}/v2.0`) を使用してください。`/common` はクロステナントサインインを許可するため、個人利用アプリには不適切です。

### 1.2 GitHub OIDC 設定（デプロイ用）

GitHub Actions から Azure へパスワードレスでデプロイするための設定です。

```bash
# サービスプリンシパル作成（Federated Credentials 使用）
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

az ad app create --display-name "xvc-github-actions"
APP_ID=$(az ad app list --display-name "xvc-github-actions" --query "[0].appId" -o tsv)

az ad sp create --id $APP_ID
SP_OBJECT_ID=$(az ad sp show --id $APP_ID --query id -o tsv)

# サブスクリプションへの権限付与
az role assignment create \
  --assignee $SP_OBJECT_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID

# Federated Credentials 設定（main ブランチ用）
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:t-develo/x_video_collector:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### 1.3 GitHub Secrets の設定

```bash
REPO="t-develo/x_video_collector"

gh secret set AZURE_CLIENT_ID --body "$APP_ID" --repo $REPO
gh secret set AZURE_TENANT_ID --body "$TENANT_ID" --repo $REPO
gh secret set AZURE_SUBSCRIPTION_ID --body "$SUBSCRIPTION_ID" --repo $REPO
gh secret set SQL_ADMIN_PASSWORD --body "<強力なパスワード>" --repo $REPO
```

Static Web Apps の API キーはインフラデプロイ後に Bicep の出力から取得します：

```bash
# インフラデプロイ後に実行
API_KEY=$(az staticwebapp secrets list \
  --name "xvc-prod-swa" \
  --resource-group "rg-xvc-prod" \
  --query "properties.apiKey" -o tsv)

gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --body "$API_KEY" --repo $REPO
```

---

## 2. 初回インフラデプロイ（手動）

GitHub Actions が設定される前に、初回のみ手動でインフラをデプロイします。

```bash
az deployment sub create \
  --location japaneast \
  --template-file infra/main.bicep \
  --parameters infra/parameters.json \
  --parameters sqlAdminPassword="<SQLパスワード>" \
  --name xvc-initial-deploy
```

---

## 3. yt-dlp / ffmpeg バイナリの配置

Azure Functions（Windows Consumption Plan）では、バイナリをデプロイパッケージに含めます。

```bash
# yt-dlp.exe ダウンロード
curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe \
  -o src/api/XVideoCollector.Functions/yt-dlp.exe

# ffmpeg ダウンロード（Windows ビルド）
# https://www.gyan.dev/ffmpeg/builds/ から ffmpeg-release-essentials.zip を取得し
# ffmpeg.exe を以下に配置
cp /path/to/ffmpeg.exe src/api/XVideoCollector.Functions/ffmpeg.exe
```

`XVideoCollector.Functions.csproj` には既にバイナリ用の `ItemGroup` が定義されています（`Condition="Exists(...)"` 付き）。バイナリファイルを上記のパスに配置するだけでビルド時に自動的にデプロイパッケージへ含まれます。

---

## 4. CI/CD フロー

### CI（自動実行タイミング）

- `feature/sprint**` / `claude/**` ブランチへの push
- `main` への Pull Request 作成・更新

CI ジョブ：
1. **dotnet-test** — .NET ビルド + 全テスト実行
2. **js-test** — Vitest によるフロントエンドテスト
3. **build-functions** — Azure Functions の Publish（PR レビュー用にアーティファクト保存）

### Deploy（自動実行タイミング）

- `main` ブランチへの push（PR マージ後）

Deploy ジョブ：
1. **infra** — Bicep でインフラデプロイ（SWA API キーを出力として取得）
2. **deploy-functions** — ソースから直接 Publish → Azure Functions にデプロイ
3. **deploy-frontend** — infra ジョブの出力 API キーを使って Static Web Apps にデプロイ

---

## 5. 環境変数一覧

### Azure Functions アプリケーション設定

| 変数名 | 説明 | 設定方法 |
|--------|------|---------|
| `AzureWebJobsStorage` | Functions ランタイム用ストレージ | Bicep で自動設定 |
| `FUNCTIONS_EXTENSION_VERSION` | Functions ランタイムバージョン (`~4`) | Bicep で自動設定 |
| `FUNCTIONS_WORKER_RUNTIME` | ワーカーランタイム (`dotnet-isolated`) | Bicep で自動設定 |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights 接続文字列 | Bicep で自動設定 |
| `ConnectionStrings__SqlDb` | Azure SQL 接続文字列 | Bicep で自動設定 |
| `BlobStorage__ConnectionString` | Blob Storage 接続文字列 | Bicep で自動設定 |
| `BlobStorage__VideoContainerName` | 動画コンテナ名 (`videos`) | Bicep で自動設定 |
| `BlobStorage__ThumbnailContainerName` | サムネイルコンテナ名 (`thumbnails`) | Bicep で自動設定 |
| `YtDlp__ExecutablePath` | yt-dlp.exe のパス | Bicep で自動設定 |
| `YtDlp__FfmpegPath` | ffmpeg.exe のパス | Bicep で自動設定 |
| `YtDlp__TimeoutSeconds` | ダウンロードタイムアウト秒数 | Bicep で自動設定 |
| `YtDlp__MaxFileSizeMB` | 最大ファイルサイズ MB | Bicep で自動設定 |

### GitHub Secrets

| シークレット名 | 説明 |
|---------------|------|
| `AZURE_CLIENT_ID` | GitHub Actions 用サービスプリンシパルの Client ID |
| `AZURE_TENANT_ID` | Entra ID テナント ID |
| `AZURE_SUBSCRIPTION_ID` | Azure サブスクリプション ID |
| `SQL_ADMIN_PASSWORD` | SQL Server 管理者パスワード |

> **注:** Static Web Apps の API トークンは Bicep デプロイの出力から自動取得するため、GitHub Secret への手動登録は不要です。

### GitHub Variables（オプション）

| 変数名 | 説明 | デフォルト |
|--------|------|-----------|
| `AZURE_LOCATION` | デプロイリージョン | `japaneast` |

---

## 6. トラブルシューティング

### CI が失敗する

**dotnet-test ジョブが失敗する場合:**
```bash
# ローカルでテストを実行して確認
dotnet test tests/XVideoCollector.Domain.Tests/XVideoCollector.Domain.Tests.csproj
dotnet test tests/XVideoCollector.Application.Tests/XVideoCollector.Application.Tests.csproj
```

**js-test ジョブが失敗する場合:**
```bash
# ローカルで確認
npm test
```

### デプロイが失敗する

**Azure Login が失敗する場合:**
- GitHub Secrets の `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` を確認
- Federated Credentials の `subject` が正しいブランチ・リポジトリを指しているか確認

**Bicep デプロイが失敗する場合:**
```bash
# What-if でデプロイ内容を事前確認
az deployment sub what-if \
  --location japaneast \
  --template-file infra/main.bicep \
  --parameters infra/parameters.json \
  --parameters sqlAdminPassword="<パスワード>"
```

**SQL Free Tier の制限:**
- Azure SQL Free Tier は 1 サブスクリプションにつき 1 データベースのみ
- 既存の Free Tier データベースがある場合は、`infra/parameters.json` に `"sqlUseFreeLimit": { "value": false }` を追加するか、デプロイ時に `sqlUseFreeLimit=false` を渡す。この場合 Basic SKU (5 DTU, 2GB) が使用される

**yt-dlp / ffmpeg が動作しない場合:**
- Azure Functions (Consumption Plan, Windows) では実行可能ファイルを `wwwroot` に配置する
- ファイルのパスを Azure Portal の Kudu コンソールで確認: `D:\home\site\wwwroot\`
- アプリ設定 `YtDlp__ExecutablePath` と `YtDlp__FfmpegPath` が正しいパスを指しているか確認

**Static Web Apps の認証が機能しない場合:**
- `staticwebapp.config.json` の `openIdIssuer` に正しいテナント ID が設定されているか確認
- Entra ID アプリの リダイレクト URI に `https://<your-swa>.azurestaticapps.net/.auth/login/aad/callback` が登録されているか確認
