#!/usr/bin/env bash
# X Video Collector — アンインストール
#
# サービスを停止・無効化し、アプリと systemd ユニットを削除する。
# データ（DB・動画・設定）は既定で残す。完全に削除する場合は --purge を指定する。
#
#   sudo bash scripts/raspi/uninstall.sh
#   sudo bash scripts/raspi/uninstall.sh --purge

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BOLD='\033[1m'; NC='\033[0m'
success() { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
err()     { echo -e "${RED}[FAIL]${NC}  $*" >&2; }
step()    { echo -e "\n${BOLD}━━━ $* ━━━${NC}"; }

APP_DIR="/opt/xvideocollector"
DATA_DIR="/var/lib/xvideocollector"
CONFIG_DIR="/etc/xvideocollector"
XVC_USER="xvc"
PURGE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --purge) PURGE=1; shift ;;
    -h|--help) echo "使い方: sudo bash scripts/raspi/uninstall.sh [--purge]"; exit 0 ;;
    *) err "不明なオプション: $1"; exit 1 ;;
  esac
done

if [[ $EUID -ne 0 ]]; then
  err "root で実行してください: sudo bash scripts/raspi/uninstall.sh"
  exit 1
fi

if [[ $PURGE -eq 1 ]]; then
  echo -e "${RED}${BOLD}警告: --purge はデータベースと保存済みの動画をすべて削除します。${NC}"
  echo -n "削除対象: ${DATA_DIR}, ${CONFIG_DIR} — 続行しますか? [yes/N]: "
  read -r answer
  if [[ "$answer" != "yes" ]]; then
    echo "中止しました。"
    exit 0
  fi
fi

step "サービス停止と自動起動の解除"
for unit in xvideocollector.service \
            xvideocollector-ytdlp-update.timer xvideocollector-backup.timer; do
  systemctl disable --now "$unit" 2>/dev/null || true
done
success "サービスを停止し、自動起動を解除しました"

step "systemd ユニットの削除"
rm -f /etc/systemd/system/xvideocollector.service \
      /etc/systemd/system/xvideocollector-ytdlp-update.service \
      /etc/systemd/system/xvideocollector-ytdlp-update.timer \
      /etc/systemd/system/xvideocollector-backup.service \
      /etc/systemd/system/xvideocollector-backup.timer
rm -rf /etc/systemd/system/xvideocollector.service.d
systemctl daemon-reload
success "systemd ユニットを削除しました"

step "アプリケーションの削除"
rm -rf "$APP_DIR"
success "${APP_DIR} を削除しました"

if [[ $PURGE -eq 1 ]]; then
  step "データと設定の削除"
  # メディアの実体が外部マウント上にある場合も削除される点に注意
  MEDIA_PATH="$(grep -oP '(?<=^LocalStorage__RootPath=).*' "${CONFIG_DIR}/xvideocollector.env" 2>/dev/null || true)"
  rm -rf "$DATA_DIR" "$CONFIG_DIR"
  if [[ -n "$MEDIA_PATH" && "$MEDIA_PATH" != "$DATA_DIR"* ]]; then
    rm -rf "$MEDIA_PATH"
    success "メディアディレクトリ ${MEDIA_PATH} を削除しました"
  fi
  userdel "$XVC_USER" 2>/dev/null || true
  success "データ・設定・ユーザーを削除しました"
else
  warn "データは残しています: ${DATA_DIR}, ${CONFIG_DIR}"
  warn "完全に削除する場合は --purge を付けて再実行してください。"
fi

echo -e "\n${GREEN}アンインストールが完了しました。${NC}\n"
