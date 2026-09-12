#!/usr/bin/env bash
# scripts/raspi/_common.sh のユニット同期と設定読み出しを検証する。
#
# systemd / aarch64 が無い環境（CI の ubuntu-latest や開発機）でも動くよう、
# ユニットの配置先は XVC_UNIT_DIR で差し替え、systemctl 呼び出しはスタブ化する。
#
#   bash tests/shell/raspi-scripts.test.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TEMPLATE_DIR="${REPO_ROOT}/scripts/raspi/systemd"

PASS=0
FAIL=0

ok()   { echo "  ok   - $*"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL - $*" >&2; FAIL=$((FAIL + 1)); }

assert_eq() {
  local expected="$1" actual="$2" label="$3"
  if [[ "$expected" == "$actual" ]]; then
    ok "$label"
  else
    fail "${label} (期待: '${expected}' / 実際: '${actual}')"
  fi
}

assert_file_exists() {
  [[ -f "$1" ]] && ok "$2" || fail "$2 (存在しない: $1)"
}

assert_file_absent() {
  [[ ! -e "$1" ]] && ok "$2" || fail "$2 (残っている: $1)"
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

export XVC_UNIT_DIR="${WORK}/units"
mkdir -p "$XVC_UNIT_DIR"

# shellcheck source=scripts/raspi/_common.sh
source "${REPO_ROOT}/scripts/raspi/_common.sh"

# render_unit が参照する導入時の値
XVC_USER="xvc"
APP_DIR="/opt/xvideocollector"
DATA_DIR="/var/lib/xvideocollector"
CONFIG_DIR="/etc/xvideocollector"
SCRIPT_INSTALL_DIR="/opt/xvideocollector/scripts"
DOTNET_BIN="/opt/dotnet/dotnet"
YTDLP_BIN="/usr/local/bin/yt-dlp"
REPO_ROOT_RENDER="/home/pi/x_video_collector"
REPO_ROOT="$REPO_ROOT_RENDER"

# systemctl はこの環境に無い / 使えないためスタブ化して呼び出しを記録する
SYSTEMCTL_LOG="${WORK}/systemctl.log"
: > "$SYSTEMCTL_LOG"
xvc_daemon_reload() { echo "daemon-reload" >> "$SYSTEMCTL_LOG"; }
xvc_enable_unit()   { echo "enable $1"     >> "$SYSTEMCTL_LOG"; }

TEMPLATE_COUNT="$(find "$TEMPLATE_DIR" -maxdepth 1 -type f | wc -l)"
TIMER_COUNT="$(find "$TEMPLATE_DIR" -maxdepth 1 -type f -name '*.timer' | wc -l)"

echo "sync_systemd_units — 初回導入"
RESULT="$(sync_systemd_units "$TEMPLATE_DIR" "${WORK}/backup1")"
assert_eq "新規=${TEMPLATE_COUNT} 更新=0" "$RESULT" "全テンプレートが新規として配置される"
assert_eq "$TEMPLATE_COUNT" "$(find "$XVC_UNIT_DIR" -maxdepth 1 -type f | wc -l)" "配置ファイル数が一致する"
assert_eq "$TIMER_COUNT" "$(grep -c '^enable ' "$SYSTEMCTL_LOG")" "タイマーだけが enable される"
assert_eq "1" "$(grep -c '^daemon-reload$' "$SYSTEMCTL_LOG")" "daemon-reload が1回呼ばれる"

echo "sync_systemd_units — 冪等性"
: > "$SYSTEMCTL_LOG"
RESULT="$(sync_systemd_units "$TEMPLATE_DIR" "${WORK}/backup2")"
assert_eq "新規=0 更新=0" "$RESULT" "2回目は変更なしになる"
assert_eq "0" "$(wc -l < "$SYSTEMCTL_LOG")" "変更が無ければ systemctl を呼ばない"
assert_file_absent "${WORK}/backup2" "変更が無ければ退避ディレクトリを作らない"

echo "sync_systemd_units — 差分検出"
: > "$SYSTEMCTL_LOG"
CHANGED_UNIT="${XVC_UNIT_DIR}/xvideocollector-backup.timer"
ADDED_UNIT="${XVC_UNIT_DIR}/xvideocollector-update.timer"
echo "# 手で書き換えた" >> "$CHANGED_UNIT"
rm -f "$ADDED_UNIT"

RESULT="$(sync_systemd_units "$TEMPLATE_DIR" "${WORK}/backup3")"
assert_eq "新規=1 更新=1" "$RESULT" "改変1件と欠落1件だけが対象になる"
grep -q "手で書き換えた" "$CHANGED_UNIT" \
  && fail "改変されたユニットがテンプレート内容へ戻る" \
  || ok "改変されたユニットがテンプレート内容へ戻る"
assert_file_exists "$ADDED_UNIT" "欠落していたユニットが再配置される"
assert_eq "enable xvideocollector-update.timer" "$(grep '^enable ' "$SYSTEMCTL_LOG")" \
  "新規のタイマーだけ enable される（既存タイマーは触らない）"
assert_file_exists "${WORK}/backup3/xvideocollector-backup.timer" "置換前の内容が退避される"
assert_eq "xvideocollector-update.timer" "$(cat "${WORK}/backup3/.added")" "新規追加分が記録される"

echo "restore_systemd_units — ロールバック"
: > "$SYSTEMCTL_LOG"
restore_systemd_units "${WORK}/backup3"
grep -q "手で書き換えた" "$CHANGED_UNIT" \
  && ok "退避した内容が書き戻される" \
  || fail "退避した内容が書き戻される"
assert_file_absent "$ADDED_UNIT" "新規追加分が削除される"
assert_eq "1" "$(grep -c '^daemon-reload$' "$SYSTEMCTL_LOG")" "書き戻し後に daemon-reload される"

echo "render_unit — プレースホルダ"
if grep -rn '__XVC_' "$XVC_UNIT_DIR" >/dev/null 2>&1; then
  grep -rn '__XVC_' "$XVC_UNIT_DIR" >&2
  fail "置換されていないプレースホルダが残っていない"
else
  ok "置換されていないプレースホルダが残っていない"
fi
assert_eq "1" "$(grep -c "ExecStart=/bin/bash ${REPO_ROOT_RENDER}/scripts/raspi/update.sh" \
  "${XVC_UNIT_DIR}/xvideocollector-update.service")" "__XVC_REPO_DIR__ がクローンのパスに置換される"

echo "設定の読み出し"
ENV_FIXTURE="${WORK}/xvideocollector.env"
cat > "$ENV_FIXTURE" <<'EOF'
ASPNETCORE_URLS=http://0.0.0.0:58180
ConnectionStrings__SqlDb=Data Source=/var/lib/xvideocollector/xvideocollector.db
LocalStorage__RootPath=/mnt/ssd/xvideocollector/media
YtDlp__ExecutablePath=/usr/local/bin/yt-dlp
#YtDlp__CookiesPath=/etc/xvideocollector/cookies.txt
EOF

assert_eq "/mnt/ssd/xvideocollector/media" "$(read_configured_media_path "$ENV_FIXTURE")" \
  "メディア保存先を env から読める"
assert_eq "/usr/local/bin/yt-dlp" "$(read_configured_ytdlp "$ENV_FIXTURE")" \
  "yt-dlp のパスを env から読める"
assert_eq "58180" "$(read_configured_port "$ENV_FIXTURE")" "ポートを env から読める"
assert_eq "" "$(read_env_value "$ENV_FIXTURE" YtDlp__CookiesPath)" \
  "コメントアウトされた設定は拾わない"
assert_eq "" "$(read_configured_media_path "${WORK}/does-not-exist")" \
  "env が無ければ空文字を返す"

# 値を書き換えたユニットから読み戻せること（install.sh / update.sh の引き継ぎ経路）
sed -i 's|^User=.*|User=pi|' "${XVC_UNIT_DIR}/xvideocollector.service"
assert_eq "pi" "$(read_installed_user)" "実行ユーザーを導入済みユニットから読める"
assert_eq "$DOTNET_BIN" "$(read_installed_dotnet)" "dotnet のパスを ExecStart から読める"

XVC_UNIT_DIR="${WORK}/nonexistent"
assert_eq "" "$(read_installed_user)" "ユニット未導入なら空文字を返す"
assert_eq "" "$(read_installed_dotnet)" "ユニット未導入なら dotnet も空文字を返す"

echo
echo "成功 ${PASS} 件 / 失敗 ${FAIL} 件"
[[ $FAIL -eq 0 ]]
