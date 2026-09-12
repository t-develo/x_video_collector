#!/usr/bin/env bash
# X Video Collector — Raspberry Pi (arm64) スタンドアロンセットアップ
#
# 実行すると以下を行う:
#   1. 前提パッケージ (.NET 10 / ffmpeg / yt-dlp) の導入
#   2. 専用ユーザー・ディレクトリ・設定ファイルの作成
#   3. アプリケーションの発行 (/opt/xvideocollector)
#   4. systemd サービス登録と自動起動の有効化
#
# 使い方:
#   sudo bash scripts/raspi/install.sh
#   sudo bash scripts/raspi/install.sh --media-path /mnt/ssd/videos --port 8080
#
# 詳細: docs/raspberry-pi.md

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'

info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
err()     { echo -e "${RED}[FAIL]${NC}  $*" >&2; }
step()    { echo -e "\n${BOLD}━━━ $* ━━━${NC}"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# shellcheck source=scripts/raspi/_common.sh
source "${SCRIPT_DIR}/_common.sh"

# ── 既定値 ─────────────────────────────────────────────────
XVC_USER=""
APP_DIR="/opt/xvideocollector"
DATA_DIR="/var/lib/xvideocollector"
CONFIG_DIR="/etc/xvideocollector"
SCRIPT_INSTALL_DIR="/opt/xvideocollector/scripts"
DOTNET_ROOT_DIR="/opt/dotnet"
MEDIA_PATH=""
PORT="8080"
PORT_EXPLICIT=0
SKIP_DEPS=0
AUTO_UPDATE=1

usage() {
  cat <<'USAGE'
使い方: sudo bash scripts/raspi/install.sh [オプション]

オプション:
  --media-path <PATH>   動画の保存先ディレクトリ (既定: /var/lib/xvideocollector/media)
                        外付け SSD/USB を使う場合はそのマウントポイント配下を指定する
  --port <PORT>         待ち受けポート (既定: 8080)
  --user <NAME>         サービス実行ユーザー (既定: xvc)
  --skip-deps           .NET / ffmpeg / yt-dlp のインストールをスキップする
  --no-auto-update      origin/main を監視する自動更新タイマーを有効化しない
                        (既定: 有効。後から sudo systemctl disable --now
                         xvideocollector-update.timer で止められる)
  -h, --help            このヘルプを表示する
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --media-path) MEDIA_PATH="$2"; shift 2 ;;
    --port)       PORT="$2"; PORT_EXPLICIT=1; shift 2 ;;
    --user)       XVC_USER="$2"; shift 2 ;;
    --skip-deps)  SKIP_DEPS=1; shift ;;
    --no-auto-update) AUTO_UPDATE=0; shift ;;
    -h|--help)    usage; exit 0 ;;
    *) err "不明なオプション: $1"; usage; exit 1 ;;
  esac
done

if [[ ! "$PORT" =~ ^[0-9]+$ ]] || (( PORT < 1 || PORT > 65535 )); then
  err "--port には 1〜65535 の数値を指定してください: ${PORT}"
  exit 1
fi

ENV_FILE="${CONFIG_DIR}/xvideocollector.env"

# 再インストール時、既存の env とユニットは導入時の指定をそのまま保持している。
# 明示指定が無ければそこから引き継ぐ（素の引数で再実行しても構成を壊さないため）。

# ポート: 確認と疎通確認をズレさせないため既存 env に合わせる
if [[ $PORT_EXPLICIT -eq 0 && -f "$ENV_FILE" ]]; then
  PORT="$(read_configured_port "$ENV_FILE")"
fi

# メディア保存先: 既定値に戻すと外部マウント用の drop-in が消えてしまう
if [[ -z "$MEDIA_PATH" ]]; then
  MEDIA_PATH="$(read_configured_media_path "$ENV_FILE")"
  [[ -n "$MEDIA_PATH" ]] && info "メディア保存先を既存の設定から引き継ぎます: ${MEDIA_PATH}"
fi
MEDIA_PATH="${MEDIA_PATH:-${DATA_DIR}/media}"

# 実行ユーザー: 既定値に戻すとファイルの所有者と噛み合わなくなる
if [[ -z "$XVC_USER" ]]; then
  XVC_USER="$(read_installed_user)"
  [[ -n "$XVC_USER" ]] && info "サービス実行ユーザーを既存のユニットから引き継ぎます: ${XVC_USER}"
fi
XVC_USER="${XVC_USER:-xvc}"

# ── 事前チェック ───────────────────────────────────────────
step "事前チェック"

if [[ $EUID -ne 0 ]]; then
  err "root で実行してください: sudo bash scripts/raspi/install.sh"
  exit 1
fi

ARCH="$(uname -m)"
if [[ "$ARCH" != "aarch64" && "$ARCH" != "arm64" ]]; then
  err "このスクリプトは 64bit ARM (aarch64) 専用です。検出したアーキテクチャ: ${ARCH}"
  err "32bit OS を使用している場合は 64bit 版 Raspberry Pi OS へ入れ替えてください。"
  exit 1
fi
success "アーキテクチャ: ${ARCH}"

if ! command -v systemctl &>/dev/null; then
  err "systemd が見つかりません。このスクリプトは systemd 環境専用です。"
  exit 1
fi
success "systemd 検出"

# 自動更新タイマーが同時に走ると発行先 (${APP_DIR}) を奪い合う。
# インストールは人が待っている操作なので、見送らずエラーで止める。
if ! acquire_update_lock; then
  err "更新処理が実行中です。完了を待ってからやり直してください。"
  err "  状況確認: systemctl status xvideocollector-update"
  exit 1
fi

# 依存導入や publish（数分かかる）の前にポートの空きを確認する。
# 塞がったまま進めると最後の systemctl enable --now で必ず失敗する。
# 稼働中・再起動ループ中のどちらも止める。
# （発行中に旧プロセスが動いたままだと DLL を上書きすることになる）
if [[ -f "${XVC_UNIT_DIR}/${XVC_SERVICE}" ]]; then
  info "既存の ${XVC_SERVICE} を停止します（再インストールのため）"
  systemctl stop "$XVC_SERVICE" 2>/dev/null || true
fi

if ! ensure_port_free "$PORT"; then
  exit 1
fi
success "ポート ${PORT} は空いています"

# ── 依存パッケージ ─────────────────────────────────────────
if [[ $SKIP_DEPS -eq 0 ]]; then
  step "依存パッケージ (ffmpeg / ffprobe / sqlite3 / curl)"
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  # ffmpeg パッケージには ffprobe も含まれる
  apt-get install -y -qq ffmpeg sqlite3 curl ca-certificates
  success "ffmpeg $(ffmpeg -version 2>/dev/null | head -1 | cut -d' ' -f3), sqlite3 $(sqlite3 --version | cut -d' ' -f1)"

  step ".NET 10 ランタイム"
  if [[ -x "${DOTNET_ROOT_DIR}/dotnet" ]] && "${DOTNET_ROOT_DIR}/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
    success ".NET 10 SDK は既にインストール済み (${DOTNET_ROOT_DIR})"
  else
    info ".NET 10 SDK を ${DOTNET_ROOT_DIR} に導入中 (数分かかります)..."
    TMP_INSTALL="$(mktemp -d)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${TMP_INSTALL}/dotnet-install.sh"
    # 発行 (dotnet publish) を行うため SDK を入れる
    bash "${TMP_INSTALL}/dotnet-install.sh" \
      --channel 10.0 --architecture arm64 --install-dir "${DOTNET_ROOT_DIR}"
    rm -rf "${TMP_INSTALL}"
    success ".NET $(${DOTNET_ROOT_DIR}/dotnet --version) インストール完了"
  fi

  step "yt-dlp"
  if [[ -x /usr/local/bin/yt-dlp ]]; then
    success "yt-dlp $(/usr/local/bin/yt-dlp --version 2>/dev/null || echo '?') — 既にインストール済み"
  else
    info "yt-dlp (linux aarch64) をダウンロード中..."
    curl -fsSL \
      "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux_aarch64" \
      -o /usr/local/bin/yt-dlp
    chmod 755 /usr/local/bin/yt-dlp
    success "yt-dlp $(/usr/local/bin/yt-dlp --version) インストール完了"
  fi
else
  warn "--skip-deps が指定されたため依存パッケージの導入をスキップします"
fi

DOTNET_BIN="${DOTNET_ROOT_DIR}/dotnet"
if [[ ! -x "$DOTNET_BIN" ]]; then
  DOTNET_BIN="$(command -v dotnet || true)"
fi
if [[ -z "$DOTNET_BIN" || ! -x "$DOTNET_BIN" ]]; then
  err "dotnet が見つかりません。--skip-deps を外して再実行してください。"
  exit 1
fi

YTDLP_BIN="$(command -v yt-dlp || echo /usr/local/bin/yt-dlp)"

# ── ユーザーとディレクトリ ─────────────────────────────────
step "ユーザーとディレクトリ"

if id -u "$XVC_USER" &>/dev/null; then
  success "ユーザー ${XVC_USER} は既に存在します"
else
  useradd --system --no-create-home --shell /usr/sbin/nologin "$XVC_USER"
  success "システムユーザー ${XVC_USER} を作成しました"
fi

mkdir -p "$APP_DIR" "$CONFIG_DIR" "$SCRIPT_INSTALL_DIR"
mkdir -p "$DATA_DIR" "$DATA_DIR/tmp" "$DATA_DIR/backups" "$MEDIA_PATH"

chown -R "$XVC_USER:$XVC_USER" "$DATA_DIR" "$MEDIA_PATH"
chmod 750 "$DATA_DIR"
success "ディレクトリを作成しました (アプリ=${APP_DIR}, データ=${DATA_DIR}, メディア=${MEDIA_PATH})"

# ── 設定ファイル ───────────────────────────────────────────
step "設定ファイル"

if [[ -f "$ENV_FILE" ]]; then
  # 既存の env は上書きしないが、--port を明示した場合は待ち受けポートだけ反映する。
  # ここで反映しないと、スクリプトは新しいポートを見ているのにアプリは古いポートで
  # 起動し、「空きを確認したはずのポートとは別のポートで競合する」ことになる。
  CURRENT_PORT="$(read_configured_port "$ENV_FILE")"
  if [[ "$CURRENT_PORT" != "$PORT" ]]; then
    set_configured_port "$ENV_FILE" "$PORT"
    chown root:"$XVC_USER" "$ENV_FILE"
    chmod 640 "$ENV_FILE"
    success "待ち受けポートを ${CURRENT_PORT} → ${PORT} に変更しました (${ENV_FILE})"
  fi

  warn "${ENV_FILE} は既に存在するため上書きしません（ポート以外の設定は保持されます、ポート=${PORT}）"
else
  SIGNING_KEY="$(openssl rand -base64 32 2>/dev/null || head -c 32 /dev/urandom | base64)"

  sed \
    -e "s|__XVC_SIGNING_KEY__|${SIGNING_KEY}|" \
    -e "s|^ASPNETCORE_URLS=.*|ASPNETCORE_URLS=http://0.0.0.0:${PORT}|" \
    -e "s|^TMPDIR=.*|TMPDIR=${DATA_DIR}/tmp|" \
    -e "s|^ConnectionStrings__SqlDb=.*|ConnectionStrings__SqlDb=Data Source=${DATA_DIR}/xvideocollector.db|" \
    -e "s|^LocalStorage__RootPath=.*|LocalStorage__RootPath=${MEDIA_PATH}|" \
    -e "s|^YtDlp__ExecutablePath=.*|YtDlp__ExecutablePath=${YTDLP_BIN}|" \
    "${SCRIPT_DIR}/xvideocollector.env.example" > "$ENV_FILE"

  chown root:"$XVC_USER" "$ENV_FILE"
  chmod 640 "$ENV_FILE"
  success "${ENV_FILE} を作成しました（署名キーは自動生成）"
fi

install -m 750 -o root -g "$XVC_USER" "${SCRIPT_DIR}/backup.sh" "${SCRIPT_INSTALL_DIR}/backup.sh"
success "バックアップスクリプトを配置しました"

# ── アプリケーションの発行 ─────────────────────────────────
step "アプリケーションの発行"

info "dotnet publish 実行中 (初回は数分かかります)..."
"$DOTNET_BIN" publish "${REPO_ROOT}/src/api/XVideoCollector.LocalHost/XVideoCollector.LocalHost.csproj" \
  --configuration Release \
  --runtime linux-arm64 \
  --self-contained false \
  --output "$APP_DIR" \
  --nologo \
  -v quiet

chown -R root:"$XVC_USER" "$APP_DIR"
chmod -R g+rX "$APP_DIR"
success "発行完了: ${APP_DIR}"

# ── systemd ユニット ───────────────────────────────────────
step "systemd サービス登録"

# render_unit は _common.sh 提供（update.sh からも使うため共通化してある）
for unit in "${SCRIPT_DIR}"/systemd/*; do
  render_unit "$unit" "${XVC_UNIT_DIR}/$(basename "$unit")"
done

# メディアが別マウントの場合、マウント完了を待ってから起動させる
MEDIA_MOUNT="$(findmnt -no TARGET --target "$MEDIA_PATH" 2>/dev/null || echo /)"
DROPIN_DIR="${XVC_UNIT_DIR}/xvideocollector.service.d"
if [[ "$MEDIA_MOUNT" != "/" ]]; then
  mkdir -p "$DROPIN_DIR"
  cat > "${DROPIN_DIR}/mount.conf" <<EOF
[Unit]
RequiresMountsFor=${MEDIA_MOUNT}

[Service]
ReadWritePaths=${MEDIA_PATH}
EOF
  success "外部マウント ${MEDIA_MOUNT} を待つ設定を追加しました"
else
  rm -f "${DROPIN_DIR}/mount.conf" 2>/dev/null || true
fi

systemctl daemon-reload
success "systemd ユニットを配置しました"

# ── 起動と自動起動の有効化 ─────────────────────────────────
step "サービス起動 / 自動起動の有効化"

# 依存導入と発行に数分かかるため、その間に別のプロセスがポートを取っている場合がある。
# 起動直前に取り直して確認し、スタックトレースではなく占有プロセス名で失敗させる。
if ! ensure_port_free "$PORT"; then
  err "発行は完了しています。ポートを空けてから次のコマンドで起動してください:"
  err "  sudo systemctl enable --now ${XVC_SERVICE}"
  exit 1
fi

# enable --now = 今すぐ起動 + 次回以降のブート時に自動起動
if ! systemctl enable --now "$XVC_SERVICE"; then
  err "サービスの起動に失敗しました。原因は次のログを参照してください:"
  dump_service_diagnostics "$XVC_SERVICE" "$PORT"
  stop_failed_service
  exit 1
fi
systemctl enable --now xvideocollector-ytdlp-update.timer
systemctl enable --now xvideocollector-backup.timer

# 自動更新はこのクローンを起点に動くため、git リポジトリでないと成立しない
if [[ $AUTO_UPDATE -eq 1 && ! -d "${REPO_ROOT}/.git" ]]; then
  warn "${REPO_ROOT} が git リポジトリではないため、自動更新は有効化しません"
  AUTO_UPDATE=0
fi

if [[ $AUTO_UPDATE -eq 1 ]]; then
  systemctl enable --now xvideocollector-update.timer
else
  # 再インストール時に以前有効化されたタイマーが残らないようにする
  systemctl disable --now xvideocollector-update.timer 2>/dev/null || true
fi
success "サービスを起動し、再起動時の自動起動を有効化しました"

# ── 疎通確認 ───────────────────────────────────────────────
step "疎通確認"

HEALTH_URL="http://127.0.0.1:${PORT}/api/health"
HEALTH_OK=0
for _ in $(seq 1 30); do
  if curl -sf -o /dev/null "$HEALTH_URL"; then HEALTH_OK=1; break; fi

  # クラッシュして再起動を繰り返している場合は 60 秒待たずに打ち切る。
  # auto-restart 中の状態は "activating" なので、状態だけでは判別できない。
  # 直前に起動したばかりなので、再起動回数が 1 以上ならクラッシュしている。
  SERVICE_STATE="$(systemctl is-active "$XVC_SERVICE" || true)"
  RESTART_COUNT="$(systemctl show -p NRestarts --value "$XVC_SERVICE" 2>/dev/null || echo 0)"
  if [[ "$SERVICE_STATE" == "failed" || "${RESTART_COUNT:-0}" -gt 0 ]]; then
    err "サービスの起動に失敗しています (状態: ${SERVICE_STATE}, 再起動回数: ${RESTART_COUNT})"
    dump_service_diagnostics "$XVC_SERVICE" "$PORT"
    stop_failed_service
    exit 1
  fi

  sleep 2
done

if [[ $HEALTH_OK -eq 1 ]]; then
  success "ヘルスチェック OK"
else
  err "ヘルスチェックに失敗しました:"
  curl -s "$HEALTH_URL" || true
  echo
  dump_service_diagnostics "$XVC_SERVICE" "$PORT"
  exit 1
fi

# ── 完了 ───────────────────────────────────────────────────
HOSTNAME_SHORT="$(hostname)"
LAN_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"

if [[ $AUTO_UPDATE -eq 1 ]]; then
  AUTO_UPDATE_NOTE="有効（origin/main を日次で監視）
    ログ      sudo journalctl --unit=xvideocollector-update
    無効化    sudo systemctl disable --now xvideocollector-update.timer"
else
  AUTO_UPDATE_NOTE="無効
    有効化    sudo systemctl enable --now xvideocollector-update.timer"
fi

step "セットアップ完了"
cat <<EOF

  ${BOLD}アクセス URL${NC}
    http://${LAN_IP:-<このマシンのIP>}:${PORT}
    http://${HOSTNAME_SHORT}.local:${PORT}   (avahi-daemon が動いている場合)

  ${BOLD}主なパス${NC}
    アプリ    ${APP_DIR}
    設定      ${CONFIG_DIR}/xvideocollector.env
    データ    ${DATA_DIR}
    メディア  ${MEDIA_PATH}

  ${BOLD}よく使うコマンド${NC}
    状態確認  sudo systemctl status xvideocollector
    ログ追跡  sudo journalctl --unit=xvideocollector -f
    再起動    sudo systemctl restart xvideocollector
    手動更新  sudo bash scripts/raspi/update.sh

  ${BOLD}自動更新${NC}
    ${AUTO_UPDATE_NOTE}

  ${BOLD}推奨: LAN 内に限定する（認証を掛けていないため）${NC}
    sudo apt install -y ufw
    sudo ufw allow from 192.168.0.0/16 to any port ${PORT} proto tcp
    sudo ufw enable

  詳細は docs/raspberry-pi.md を参照してください。

EOF
