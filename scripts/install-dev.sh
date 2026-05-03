#!/usr/bin/env bash
# X Video Collector — ローカル開発環境セットアップ (sudo 不要版)
# Ubuntu 24.04 向け

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'

info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
step()    { echo -e "\n${BOLD}━━━ $* ━━━${NC}"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
FUNCTIONS_DIR="${REPO_ROOT}/src/api/XVideoCollector.Functions"

LOCAL_BIN="$HOME/.local/bin"
NPM_GLOBAL="$HOME/.npm-global"

mkdir -p "$LOCAL_BIN" "$NPM_GLOBAL"

# PATH に追加 (このスクリプト内)
export PATH="$NPM_GLOBAL/bin:$LOCAL_BIN:$HOME/.dotnet/tools:$PATH"

# ── 1. npm グローバルプレフィックスをユーザーローカルに設定 ─────
step "npm グローバルプレフィックス設定"
npm config set prefix "$NPM_GLOBAL"
success "npm prefix → $NPM_GLOBAL"

# ── 2. Azure Functions Core Tools v4 ─────────────────────────
step "Azure Functions Core Tools v4"
if command -v func &>/dev/null && func --version 2>/dev/null | grep -qE "^4\."; then
  success "func $(func --version) — 既にインストール済み"
else
  info "Azure Functions Core Tools v4 をインストール中..."
  npm install -g azure-functions-core-tools@4 --unsafe-perm true
  success "func $(func --version) インストール完了"
fi

# ── 3. Azurite ─────────────────────────────────────────────
step "Azurite (Azure Storage エミュレーター)"
if command -v azurite &>/dev/null; then
  success "azurite — 既にインストール済み"
else
  info "Azurite をインストール中..."
  npm install -g azurite
  success "Azurite インストール完了"
fi

# ── 4. dotnet-ef ───────────────────────────────────────────
step "dotnet-ef (EF Core CLI ツール)"
if dotnet tool list -g 2>/dev/null | grep -q "dotnet-ef"; then
  success "dotnet-ef — 既にインストール済み"
else
  info "dotnet-ef をインストール中..."
  dotnet tool install -g dotnet-ef
  success "dotnet-ef インストール完了"
fi

# ── 5. yt-dlp ──────────────────────────────────────────────
step "yt-dlp (Linux 版)"
if command -v yt-dlp &>/dev/null; then
  success "yt-dlp $(yt-dlp --version) — 既にインストール済み"
else
  info "yt-dlp をインストール中..."
  curl -fsSL \
    "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp" \
    -o "$LOCAL_BIN/yt-dlp"
  chmod +x "$LOCAL_BIN/yt-dlp"
  success "yt-dlp $("$LOCAL_BIN/yt-dlp" --version) インストール完了"
fi

# ── 6. ffmpeg (静的ビルド) ─────────────────────────────────
step "ffmpeg (静的ビルド)"
if command -v ffmpeg &>/dev/null; then
  success "ffmpeg — 既にインストール済み"
else
  info "ffmpeg 静的ビルドをダウンロード中..."
  FFMPEG_TMP="$(mktemp -d)"
  curl -fsSL \
    "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz" \
    -o "${FFMPEG_TMP}/ffmpeg.tar.xz"
  tar xf "${FFMPEG_TMP}/ffmpeg.tar.xz" -C "${FFMPEG_TMP}" --strip-components=1
  cp "${FFMPEG_TMP}/ffmpeg"  "$LOCAL_BIN/"
  cp "${FFMPEG_TMP}/ffprobe" "$LOCAL_BIN/" 2>/dev/null || true
  chmod +x "$LOCAL_BIN/ffmpeg" "$LOCAL_BIN/ffprobe" 2>/dev/null || true
  rm -rf "$FFMPEG_TMP"
  success "ffmpeg $(ffmpeg -version 2>&1 | head -1) インストール完了"
fi

# ── 7. npm 依存パッケージ ──────────────────────────────────
step "npm パッケージ (プロジェクトルート)"
cd "${REPO_ROOT}"
if [[ -d node_modules ]]; then
  success "node_modules — 既にインストール済み"
else
  info "npm install 中..."
  npm install
  success "npm install 完了"
fi

# ── 8. local.settings.json 作成 ────────────────────────────
step "local.settings.json (Azure Functions ローカル設定)"
LOCAL_SETTINGS="${FUNCTIONS_DIR}/local.settings.json"
if [[ -f "$LOCAL_SETTINGS" ]]; then
  warn "local.settings.json は既に存在します（スキップ）"
else
  info "local.settings.json を作成中..."
  cat > "$LOCAL_SETTINGS" << JSON
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SKIP_AUTH": "true",
    "BlobStorage__ConnectionString": "UseDevelopmentStorage=true",
    "BlobStorage__VideoContainerName": "videos",
    "BlobStorage__ThumbnailContainerName": "thumbnails",
    "QueueStorage__ConnectionString": "UseDevelopmentStorage=true",
    "QueueStorage__DownloadQueueName": "video-download-requests",
    "YtDlp__ExecutablePath": "$(realpath ${LOCAL_BIN}/yt-dlp)",
    "YtDlp__FfmpegPath": "$(realpath ${LOCAL_BIN}/ffmpeg)",
    "YtDlp__FfprobePath": "$(realpath ${LOCAL_BIN}/ffprobe)",
    "YtDlp__TimeoutSeconds": "300",
    "YtDlp__MaxFileSizeMB": "500"
  },
  "ConnectionStrings": {
    "SqlDb": "Data Source=xvideocollector.db"
  }
}
JSON
  success "local.settings.json 作成完了 (SQLite 使用)"
fi

# ── 9. .NET ビルド確認 ─────────────────────────────────────
step ".NET ビルド確認"
cd "${REPO_ROOT}"
dotnet build src/api/XVideoCollector.Functions/XVideoCollector.Functions.csproj \
  --configuration Debug -v quiet
success ".NET ビルド成功"

# ── 完了 ───────────────────────────────────────────────────
step "セットアップ完了"
echo -e "${GREEN}"
echo "  ✔ Azure Functions Core Tools v4 ($NPM_GLOBAL/bin/func)"
echo "  ✔ Azurite"
echo "  ✔ dotnet-ef"
echo "  ✔ yt-dlp ($LOCAL_BIN/yt-dlp)"
echo "  ✔ ffmpeg ($LOCAL_BIN/ffmpeg)"
echo "  ✔ local.settings.json (SQLite DB)"
echo "  ✔ .NET ビルド OK"
echo -e "${NC}"

echo -e "PATH に追加が必要です (未設定の場合):"
echo -e "  ${BOLD}export PATH=\"\$HOME/.npm-global/bin:\$HOME/.local/bin:\$HOME/.dotnet/tools:\$PATH\"${NC}"
echo ""
echo -e "次のステップ: ${BOLD}bash scripts/start-dev.sh${NC}"
