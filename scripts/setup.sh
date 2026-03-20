#!/usr/bin/env bash
# X Video Collector — 初期セットアップスクリプト
#
# 実行すること:
#   1. 前提条件チェック (az, gh, curl, unzip, jq)
#   2. Entra ID アプリ登録 (SWA 認証用)
#   3. GitHub Actions OIDC サービスプリンシパル + Federated Credentials 設定
#   4. GitHub Secrets 設定 (AZURE_* + SQL_ADMIN_PASSWORD)
#   5. yt-dlp.exe / ffmpeg.exe バイナリダウンロード
#   6. 初回 Bicep インフラデプロイ
#   7. Entra ID リダイレクト URI 登録
#
# 使い方:
#   bash scripts/setup.sh [--repo <owner/repo>] [--location <region>] [--skip-deploy]
#
# オプション:
#   --repo         GitHub リポジトリ (省略時: git remote から自動取得)
#   --location     Azure リージョン (省略時: japaneast)
#   --skip-deploy  Bicep デプロイをスキップ (後で手動実行したい場合)

set -euo pipefail

# ---- カラー & ヘルパー ----------------------------------------
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'

info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error()   { echo -e "${RED}[ERR ]${NC}  $*" >&2; exit 1; }
step()    { echo -e "\n${BOLD}━━━ $* ━━━${NC}"; }

# ---- 引数パース -----------------------------------------------
REPO=""
LOCATION="japaneast"
SKIP_DEPLOY=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo)     REPO="$2";     shift 2 ;;
    --location) LOCATION="$2"; shift 2 ;;
    --skip-deploy) SKIP_DEPLOY=true; shift ;;
    *) error "不明なオプション: $1" ;;
  esac
done

# ---- リポジトリルート確認 ----------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "$REPO_ROOT"
[[ -f "infra/main.bicep" ]] || error "スクリプトはリポジトリルートの scripts/ から実行してください"

# ---- 前提条件チェック -------------------------------------------
step "前提条件チェック"
for cmd in az gh curl unzip jq; do
  command -v "$cmd" &>/dev/null \
    && success "$cmd" \
    || error "$cmd がインストールされていません。インストール後に再実行してください。"
done

# ---- GitHub リポジトリ名の自動取得 --------------------------------
if [[ -z "$REPO" ]]; then
  REPO=$(git remote get-url origin 2>/dev/null \
    | sed -E 's|.*github\.com[:/]||; s|\.git$||') \
    || true
  [[ -n "$REPO" ]] \
    && info "リポジトリ: ${REPO}" \
    || error "--repo オプションでリポジトリを指定してください (例: owner/repo)"
fi

FUNCTIONS_DIR="src/api/XVideoCollector.Functions"

# ---- Azure ログイン -------------------------------------------
step "Azure 認証"
if ! az account show &>/dev/null; then
  info "az login を実行します..."
  az login
fi
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
ACCOUNT_NAME=$(az account show --query name -o tsv)
success "サブスクリプション : ${ACCOUNT_NAME} (${SUBSCRIPTION_ID})"
success "テナント ID        : ${TENANT_ID}"

# ---- GitHub ログイン ------------------------------------------
step "GitHub 認証"
if ! gh auth status &>/dev/null; then
  info "gh auth login を実行します..."
  gh auth login
fi
success "GitHub 認証済み"

# ---- SQL 管理者パスワード入力 -----------------------------------
step "SQL 管理者パスワード"
SQL_PASSWORD=""
if gh secret list --repo "$REPO" 2>/dev/null | grep -q "^SQL_ADMIN_PASSWORD"; then
  warn "SQL_ADMIN_PASSWORD は GitHub Secrets に設定済みです"
  warn "Bicep デプロイのためパスワードを入力してください（GitHub Secret の更新は行いません）"
  SAVE_SQL_SECRET=false
else
  SAVE_SQL_SECRET=true
fi

while true; do
  read -rsp "SQL 管理者パスワード (12文字以上・大文字/小文字/数字/記号を含む): " SQL_PASSWORD
  echo
  [[ ${#SQL_PASSWORD} -ge 12 ]] || { warn "12文字以上入力してください"; continue; }
  read -rsp "確認のため再入力: " SQL_PASSWORD2
  echo
  [[ "$SQL_PASSWORD" == "$SQL_PASSWORD2" ]] && break
  warn "パスワードが一致しません。再試行します。"
done

# ---- Entra ID 認証アプリ登録 ------------------------------------
step "Entra ID アプリ登録 (SWA 認証用)"
AUTH_APP_DISPLAY="X Video Collector"
AUTH_APP_ID=$(az ad app list \
  --display-name "$AUTH_APP_DISPLAY" \
  --query "[0].appId" -o tsv 2>/dev/null || true)

if [[ -n "$AUTH_APP_ID" && "$AUTH_APP_ID" != "None" ]]; then
  warn "\"${AUTH_APP_DISPLAY}\" は既に存在します (appId: ${AUTH_APP_ID})"
else
  AUTH_APP_ID=$(az ad app create \
    --display-name "$AUTH_APP_DISPLAY" \
    --sign-in-audience AzureADMyOrg \
    --query appId -o tsv)
  success "Entra ID アプリ作成 (appId: ${AUTH_APP_ID})"
fi

# staticwebapp.config.json のテナント ID プレースホルダー置換
if grep -q "__TENANT_ID__" src/frontend/staticwebapp.config.json 2>/dev/null; then
  sed -i "s/__TENANT_ID__/${TENANT_ID}/g" src/frontend/staticwebapp.config.json
  success "staticwebapp.config.json にテナント ID を設定しました"
else
  warn "staticwebapp.config.json の __TENANT_ID__ は既に置換済みです"
fi

# ---- GitHub Actions OIDC 用サービスプリンシパル -----------------
step "GitHub OIDC サービスプリンシパル設定"
OIDC_APP_DISPLAY="xvc-github-actions"
OIDC_APP_ID=$(az ad app list \
  --display-name "$OIDC_APP_DISPLAY" \
  --query "[0].appId" -o tsv 2>/dev/null || true)

if [[ -n "$OIDC_APP_ID" && "$OIDC_APP_ID" != "None" ]]; then
  warn "\"${OIDC_APP_DISPLAY}\" は既に存在します (appId: ${OIDC_APP_ID})"
else
  OIDC_APP_ID=$(az ad app create \
    --display-name "$OIDC_APP_DISPLAY" \
    --query appId -o tsv)
  success "OIDC アプリ作成 (appId: ${OIDC_APP_ID})"
fi

# サービスプリンシパル作成（冪等）
SP_OBJECT_ID=$(az ad sp show --id "$OIDC_APP_ID" --query id -o tsv 2>/dev/null || true)
if [[ -z "$SP_OBJECT_ID" || "$SP_OBJECT_ID" == "None" ]]; then
  az ad sp create --id "$OIDC_APP_ID" > /dev/null
  SP_OBJECT_ID=$(az ad sp show --id "$OIDC_APP_ID" --query id -o tsv)
  success "サービスプリンシパル作成 (objectId: ${SP_OBJECT_ID})"
else
  warn "サービスプリンシパルは既に存在します"
fi

# Contributor ロール割り当て（冪等）
ROLE_EXISTS=$(az role assignment list \
  --assignee "$SP_OBJECT_ID" \
  --role Contributor \
  --scope "/subscriptions/${SUBSCRIPTION_ID}" \
  --query "[0].id" -o tsv 2>/dev/null || true)
if [[ -n "$ROLE_EXISTS" && "$ROLE_EXISTS" != "None" ]]; then
  warn "Contributor ロールは既に割り当て済みです"
else
  az role assignment create \
    --assignee "$SP_OBJECT_ID" \
    --role Contributor \
    --scope "/subscriptions/${SUBSCRIPTION_ID}" > /dev/null
  success "Contributor ロールを割り当てました"
fi

# Federated Credential — main ブランチ用（冪等）
FC_NAME="github-main"
FC_EXISTS=$(az ad app federated-credential list \
  --id "$OIDC_APP_ID" \
  --query "[?name=='${FC_NAME}'].name" -o tsv 2>/dev/null || true)
if [[ -n "$FC_EXISTS" ]]; then
  warn "Federated Credential \"${FC_NAME}\" は既に存在します"
else
  az ad app federated-credential create \
    --id "$OIDC_APP_ID" \
    --parameters "$(jq -n \
      --arg name "$FC_NAME" \
      --arg repo "$REPO" \
      '{
        name: $name,
        issuer: "https://token.actions.githubusercontent.com",
        subject: ("repo:" + $repo + ":ref:refs/heads/main"),
        audiences: ["api://AzureADTokenExchange"]
      }')" > /dev/null
  success "Federated Credential (main ブランチ) 作成"
fi

# Federated Credential — Pull Request 用（冪等）
FC_PR_NAME="github-pr"
FC_PR_EXISTS=$(az ad app federated-credential list \
  --id "$OIDC_APP_ID" \
  --query "[?name=='${FC_PR_NAME}'].name" -o tsv 2>/dev/null || true)
if [[ -n "$FC_PR_EXISTS" ]]; then
  warn "Federated Credential \"${FC_PR_NAME}\" は既に存在します"
else
  az ad app federated-credential create \
    --id "$OIDC_APP_ID" \
    --parameters "$(jq -n \
      --arg name "$FC_PR_NAME" \
      --arg repo "$REPO" \
      '{
        name: $name,
        issuer: "https://token.actions.githubusercontent.com",
        subject: ("repo:" + $repo + ":pull_request"),
        audiences: ["api://AzureADTokenExchange"]
      }')" > /dev/null
  success "Federated Credential (Pull Request) 作成"
fi

# ---- GitHub Secrets 設定 ------------------------------------
step "GitHub Secrets 設定"
gh secret set AZURE_CLIENT_ID       --body "$OIDC_APP_ID"       --repo "$REPO"
gh secret set AZURE_TENANT_ID       --body "$TENANT_ID"          --repo "$REPO"
gh secret set AZURE_SUBSCRIPTION_ID --body "$SUBSCRIPTION_ID"    --repo "$REPO"
success "AZURE_CLIENT_ID / AZURE_TENANT_ID / AZURE_SUBSCRIPTION_ID を設定"

if [[ "$SAVE_SQL_SECRET" == "true" ]]; then
  gh secret set SQL_ADMIN_PASSWORD --body "$SQL_PASSWORD" --repo "$REPO"
  success "SQL_ADMIN_PASSWORD を設定"
fi

# ---- yt-dlp / ffmpeg バイナリダウンロード -----------------------
step "yt-dlp / ffmpeg バイナリ取得 (Windows 実行ファイル)"

# yt-dlp.exe
if [[ -f "${FUNCTIONS_DIR}/yt-dlp.exe" ]]; then
  warn "yt-dlp.exe は既に存在します（スキップ）"
else
  info "yt-dlp.exe をダウンロード中..."
  curl -fsSL \
    "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe" \
    -o "${FUNCTIONS_DIR}/yt-dlp.exe"
  success "yt-dlp.exe を配置: ${FUNCTIONS_DIR}/yt-dlp.exe"
fi

# ffmpeg.exe (BtbN Windows GPL ビルド)
if [[ -f "${FUNCTIONS_DIR}/ffmpeg.exe" ]]; then
  warn "ffmpeg.exe は既に存在します（スキップ）"
else
  info "ffmpeg.exe をダウンロード中 (Windows GPL ビルド)..."
  FFMPEG_ZIP="$(mktemp /tmp/ffmpeg-XXXXXX.zip)"
  curl -fsSL \
    "https://github.com/BtbN/ffmpeg-builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip" \
    -o "$FFMPEG_ZIP"
  unzip -jo "$FFMPEG_ZIP" "*/bin/ffmpeg.exe" -d "${FUNCTIONS_DIR}"
  rm -f "$FFMPEG_ZIP"
  success "ffmpeg.exe を配置: ${FUNCTIONS_DIR}/ffmpeg.exe"
fi

# ---- 初回インフラデプロイ ----------------------------------------
SWA_HOSTNAME=""
if [[ "$SKIP_DEPLOY" == "true" ]]; then
  warn "--skip-deploy が指定されているため Bicep デプロイをスキップします"
  warn "後で以下のコマンドで手動デプロイしてください:"
  warn "  az deployment sub create --location ${LOCATION} --template-file infra/main.bicep \\"
  warn "    --parameters infra/parameters.json --parameters sqlAdminPassword=<パスワード>"
else
  step "初回インフラデプロイ (Bicep)"
  info "デプロイを開始します（約 5〜10 分かかります）..."
  DEPLOY_NAME="xvc-initial-$(date +%Y%m%d%H%M%S)"

  DEPLOY_OUTPUT=$(az deployment sub create \
    --location "$LOCATION" \
    --template-file infra/main.bicep \
    --parameters infra/parameters.json \
    --parameters "sqlAdminPassword=${SQL_PASSWORD}" \
    --name "$DEPLOY_NAME" \
    --query "properties.outputs" -o json)

  SQL_PASSWORD=""  # メモリからクリア

  FUNCTIONS_APP_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.functionsAppName.value // empty')
  SWA_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.staticWebAppName.value // empty')
  SWA_HOSTNAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.staticWebAppHostname.value // empty')
  RG_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.resourceGroupName.value // empty')

  success "デプロイ完了"
  info "  リソースグループ : ${RG_NAME}"
  info "  Functions App   : ${FUNCTIONS_APP_NAME}"
  info "  Static Web App  : https://${SWA_HOSTNAME}"

  # ---- Entra ID リダイレクト URI 登録 ----------------------------
  step "Entra ID リダイレクト URI 登録"
  if [[ -n "$SWA_HOSTNAME" ]]; then
    REDIRECT_URI="https://${SWA_HOSTNAME}/.auth/login/aad/callback"
    AUTH_OBJECT_ID=$(az ad app show --id "$AUTH_APP_ID" --query id -o tsv)

    # 既存 URI リストに追記
    CURRENT_URIS=$(az ad app show --id "$AUTH_APP_ID" \
      --query "web.redirectUris" -o json 2>/dev/null || echo '[]')
    URI_EXISTS=$(echo "$CURRENT_URIS" | jq -r --arg u "$REDIRECT_URI" '.[] | select(. == $u)' || true)

    if [[ -n "$URI_EXISTS" ]]; then
      warn "リダイレクト URI は既に登録済みです"
    else
      UPDATED_URIS=$(echo "$CURRENT_URIS" | jq -c --arg u "$REDIRECT_URI" '. + [$u]')
      az ad app update \
        --id "$AUTH_APP_ID" \
        --web-redirect-uris "$(echo "$UPDATED_URIS" | jq -r '.[]' | tr '\n' ' ')" > /dev/null
      success "リダイレクト URI 登録: ${REDIRECT_URI}"
    fi
  else
    warn "SWA ホスト名を取得できませんでした"
    warn "Entra ID アプリ (${AUTH_APP_ID}) に以下のリダイレクト URI を手動登録してください:"
    warn "  https://<your-swa>.azurestaticapps.net/.auth/login/aad/callback"
  fi
fi

SQL_PASSWORD=""  # スキップ時もクリア

# ---- 完了サマリー ----------------------------------------------
step "セットアップ完了"
echo -e "${GREEN}"
echo "  ✔ Entra ID アプリ登録        (appId: ${AUTH_APP_ID})"
echo "  ✔ GitHub OIDC 設定           (appId: ${OIDC_APP_ID})"
echo "  ✔ GitHub Secrets 設定        (AZURE_* + SQL_ADMIN_PASSWORD)"
echo "  ✔ yt-dlp.exe / ffmpeg.exe 配置"
[[ "$SKIP_DEPLOY" == "false" ]] && echo "  ✔ 初回インフラデプロイ完了"
echo -e "${NC}"

echo -e "次のステップ:"
echo -e "  1. staticwebapp.config.json の変更をコミット:"
echo -e "     ${BOLD}git add src/frontend/staticwebapp.config.json && git commit -m 'chore: set tenant ID in SWA config'${NC}"
echo -e "  2. main ブランチへ push → Deploy ワークフローが自動実行されます"
if [[ -n "$SWA_HOSTNAME" ]]; then
  echo -e "  3. アクセス: ${BOLD}https://${SWA_HOSTNAME}${NC}"
fi
