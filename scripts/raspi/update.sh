#!/usr/bin/env bash
# X Video Collector — 更新スクリプト
#
# リポジトリを最新にして再発行し、サービスを再起動する。
# 設定ファイル (/etc/xvideocollector/xvideocollector.env) とデータは保持される。
#
#   sudo bash scripts/raspi/update.sh
#   sudo bash scripts/raspi/update.sh --no-pull   # git pull せず現在の作業ツリーで再発行

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'
info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[ OK ]${NC}  $*"; }
err()     { echo -e "${RED}[FAIL]${NC}  $*" >&2; }
step()    { echo -e "\n${BOLD}━━━ $* ━━━${NC}"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# shellcheck source=scripts/raspi/_common.sh
source "${SCRIPT_DIR}/_common.sh"

XVC_USER="xvc"
APP_DIR="/opt/xvideocollector"
CONFIG_DIR="/etc/xvideocollector"
SCRIPT_INSTALL_DIR="/opt/xvideocollector/scripts"
DOTNET_BIN="/opt/dotnet/dotnet"
DO_PULL=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-pull) DO_PULL=0; shift ;;
    -h|--help) echo "使い方: sudo bash scripts/raspi/update.sh [--no-pull]"; exit 0 ;;
    *) err "不明なオプション: $1"; exit 1 ;;
  esac
done

if [[ $EUID -ne 0 ]]; then
  err "root で実行してください: sudo bash scripts/raspi/update.sh"
  exit 1
fi

[[ -x "$DOTNET_BIN" ]] || DOTNET_BIN="$(command -v dotnet || true)"
if [[ -z "$DOTNET_BIN" || ! -x "$DOTNET_BIN" ]]; then
  err "dotnet が見つかりません。先に install.sh を実行してください。"
  exit 1
fi

if [[ ! -f "${CONFIG_DIR}/xvideocollector.env" ]]; then
  err "${CONFIG_DIR}/xvideocollector.env がありません。先に install.sh を実行してください。"
  exit 1
fi

# ── 1. 最新コードを取得 ────────────────────────────────────
if [[ $DO_PULL -eq 1 ]]; then
  step "リポジトリ更新"
  BRANCH="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)"
  git -C "$REPO_ROOT" pull --ff-only origin "$BRANCH"
  success "$(git -C "$REPO_ROOT" log -1 --oneline)"
fi

# ── 2. 再発行 ──────────────────────────────────────────────
step "アプリケーションの再発行"
info "dotnet publish 実行中..."
"$DOTNET_BIN" publish "${REPO_ROOT}/src/api/XVideoCollector.LocalHost/XVideoCollector.LocalHost.csproj" \
  --configuration Release \
  --runtime linux-arm64 \
  --self-contained false \
  --output "$APP_DIR" \
  --nologo \
  -v quiet

chown -R root:"$XVC_USER" "$APP_DIR"
chmod -R g+rX "$APP_DIR"
install -m 750 -o root -g "$XVC_USER" "${SCRIPT_DIR}/backup.sh" "${SCRIPT_INSTALL_DIR}/backup.sh"
success "発行完了"

# ── 3. 再起動と確認 ────────────────────────────────────────
step "サービス再起動"
if ! systemctl restart "$XVC_SERVICE"; then
  err "サービスの再起動に失敗しました:"
  dump_service_diagnostics
  exit 1
fi

PORT="$(read_configured_port "${CONFIG_DIR}/xvideocollector.env")"
HEALTH_URL="http://127.0.0.1:${PORT}/api/health"

for _ in $(seq 1 30); do
  if curl -sf -o /dev/null "$HEALTH_URL"; then
    success "更新完了（ヘルスチェック OK）"
    exit 0
  fi

  # クラッシュして再起動を繰り返している場合は待たずに打ち切る
  SERVICE_STATE="$(systemctl is-active "$XVC_SERVICE" || true)"
  RESTART_COUNT="$(systemctl show -p NRestarts --value "$XVC_SERVICE" 2>/dev/null || echo 0)"
  if [[ "$SERVICE_STATE" == "failed" || "${RESTART_COUNT:-0}" -gt 0 ]]; then
    err "再起動後にサービスが落ちています (状態: ${SERVICE_STATE}, 再起動回数: ${RESTART_COUNT})"
    dump_service_diagnostics
    exit 1
  fi

  sleep 2
done

err "再起動後のヘルスチェックに失敗しました:"
curl -s "$HEALTH_URL" || true
echo
dump_service_diagnostics
exit 1
