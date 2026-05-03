#!/usr/bin/env bash
# X Video Collector — ローカル開発サーバー起動スクリプト
# 以下を並行起動する:
#   1. Azurite (Azure Storage エミュレーター) — ポート 10000-10002
#   2. Azure Functions (API バックエンド) — ポート 7071
#   3. 開発用プロキシサーバー (フロントエンド) — ポート 3000

set -euo pipefail

BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'
info() { echo -e "${BLUE}[INFO]${NC}  $*"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
FUNCTIONS_DIR="${REPO_ROOT}/src/api/XVideoCollector.Functions"
AZURITE_DIR="${REPO_ROOT}/.azurite"

# PATH に dotnet tools と user local bin を追加
export PATH="$HOME/.npm-global/bin:$HOME/.local/bin:$HOME/.dotnet/tools:$PATH"

# Azurite データディレクトリ
mkdir -p "${AZURITE_DIR}"

# ── 前提チェック ───────────────────────────────────────────
for cmd in func azurite node; do
  command -v "$cmd" &>/dev/null || {
    echo "ERROR: $cmd が見つかりません。先に bash scripts/install-dev.sh を実行してください。"
    exit 1
  }
done

[[ -f "${FUNCTIONS_DIR}/local.settings.json" ]] || {
  echo "ERROR: local.settings.json が見つかりません。先に bash scripts/install-dev.sh を実行してください。"
  exit 1
}

# ── プロセス管理 ──────────────────────────────────────────
PIDS=()
cleanup() {
  echo -e "\n停止中..."
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
  wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# ── 1. Azurite ────────────────────────────────────────────
info "Azurite を起動中 (Blob :10000, Queue :10001, Table :10002)..."
azurite \
  --location "${AZURITE_DIR}" \
  --blobPort 10000 \
  --queuePort 10001 \
  --tablePort 10002 \
  --silent &
PIDS+=($!)
sleep 2

# ── 2. .NET ビルド ─────────────────────────────────────────
info ".NET プロジェクトをビルド中..."
cd "${REPO_ROOT}"
dotnet build src/api/XVideoCollector.Functions/XVideoCollector.Functions.csproj \
  --configuration Debug -v quiet
info "ビルド完了"

# ── 3. Azure Functions ────────────────────────────────────
info "Azure Functions を起動中 (http://localhost:7071)..."
cd "${FUNCTIONS_DIR}"
func start --port 7071 &
PIDS+=($!)
cd "${REPO_ROOT}"

# Functions が起動するまで待機
info "Functions 起動待機中..."
for i in {1..20}; do
  if curl -sf "http://localhost:7071/api/health" &>/dev/null; then
    info "Functions 起動確認OK"
    break
  fi
  sleep 2
done

# ── 4. 開発プロキシサーバー ────────────────────────────────
info "開発プロキシサーバーを起動中 (http://localhost:3000)..."
node "${SCRIPT_DIR}/dev-server.js" &
PIDS+=($!)

echo -e "\n${BOLD}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "  ブラウザで開く: ${BOLD}http://localhost:3000${NC}"
echo -e "  API直接アクセス: http://localhost:7071/api/health"
echo -e "  停止: Ctrl+C"
echo -e "${BOLD}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

wait
