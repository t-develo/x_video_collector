#!/usr/bin/env bash
# X Video Collector — 更新スクリプト
#
# リポジトリを最新にして再発行し、サービスを再起動する。
# 設定ファイル (/etc/xvideocollector/xvideocollector.env) とデータは保持される。
#
#   sudo bash scripts/raspi/update.sh
#   sudo bash scripts/raspi/update.sh --no-pull   # git pull せず現在の作業ツリーで再発行
#
# 自動更新 (xvideocollector-update.timer) からは次の形で呼ばれる:
#   sudo bash scripts/raspi/update.sh --branch main --if-changed --defer-while-busy --quiet
#
# オプション:
#   --branch <名前>       pull 対象のブランチを固定する（既定は現在のブランチ）
#   --check-only          更新の有無だけを判定する（0=更新あり / 1=更新なし）。発行はしない
#   --if-changed          origin に新しいコミットがある時だけ更新する
#   --defer-while-busy    ダウンロード/変換の実行中は更新を見送る
#   --quiet               進捗表示を抑える（journald 向け）
#   --no-pull             git pull せず現在の作業ツリーで再発行する

set -euo pipefail

# journald へ出力する場合に ANSI エスケープが混ざらないよう、端末以外では色を付けない
if [[ -t 1 ]]; then
  RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[0;33m'; BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'
else
  RED=''; GREEN=''; YELLOW=''; BLUE=''; BOLD=''; NC=''
fi

QUIET=0
info()    { [[ $QUIET -eq 1 ]] || echo -e "${BLUE}[INFO]${NC}  $*"; }
step()    { [[ $QUIET -eq 1 ]] || echo -e "\n${BOLD}━━━ $* ━━━${NC}"; }
success() { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*" >&2; }
err()     { echo -e "${RED}[FAIL]${NC}  $*" >&2; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# shellcheck source=scripts/raspi/_common.sh
source "${SCRIPT_DIR}/_common.sh"

APP_DIR="/opt/xvideocollector"
DATA_DIR="/var/lib/xvideocollector"
CONFIG_DIR="/etc/xvideocollector"
SCRIPT_INSTALL_DIR="/opt/xvideocollector/scripts"
# 導入時の値は後段（root チェック後）で既存の構成から復元する
XVC_USER=""
DOTNET_BIN=""
YTDLP_BIN=""

DO_PULL=1
TARGET_BRANCH=""
CHECK_ONLY=0
IF_CHANGED=0
DEFER_WHILE_BUSY=0

# 冒頭のコメントブロック（shebang の次から最初の非コメント行まで）をそのまま使う
usage() {
  awk 'NR > 1 { if ($0 !~ /^#/) exit; sub(/^# ?/, ""); print }' "${BASH_SOURCE[0]}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-pull) DO_PULL=0; shift ;;
    --branch)
      [[ $# -ge 2 ]] || { err "--branch にはブランチ名が必要です"; exit 1; }
      TARGET_BRANCH="$2"; shift 2 ;;
    --check-only) CHECK_ONLY=1; shift ;;
    --if-changed) IF_CHANGED=1; shift ;;
    --defer-while-busy) DEFER_WHILE_BUSY=1; shift ;;
    --quiet) QUIET=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) err "不明なオプション: $1"; exit 1 ;;
  esac
done

if [[ $DO_PULL -eq 0 && -n "$TARGET_BRANCH" ]]; then
  err "--no-pull と --branch は同時に指定できません"
  exit 1
fi

if [[ $DO_PULL -eq 0 && ( $CHECK_ONLY -eq 1 || $IF_CHANGED -eq 1 ) ]]; then
  err "--no-pull と --check-only / --if-changed は同時に指定できません"
  exit 1
fi

if [[ $EUID -ne 0 ]]; then
  err "root で実行してください: sudo bash scripts/raspi/update.sh"
  exit 1
fi

# ── 排他制御 ───────────────────────────────────────────────
# timer 起動・手動実行・install.sh が重なると発行先を奪い合うため、1 つだけ通す。
# 自動更新は次回の発火で仕切り直せるので、待たずに見送る。
if ! acquire_update_lock; then
  # --quiet でも journald に残るよう info ではなく echo で出す
  echo "別の更新処理が実行中のため、この実行は見送ります"
  exit 0
fi

ENV_FILE="${CONFIG_DIR}/xvideocollector.env"

if [[ ! -f "$ENV_FILE" ]]; then
  err "${ENV_FILE} がありません。先に install.sh を実行してください。"
  exit 1
fi

# ── 導入時の構成を復元 ─────────────────────────────────────
# systemd ユニットを描き直すために、install.sh が使った値を
# 導入済みユニットと env ファイルから拾い直す。
# 取れなかった場合は既定値にフォールバックする。
XVC_USER="$(read_installed_user)"
if [[ -z "$XVC_USER" ]]; then
  XVC_USER="xvc"
  warn "導入済みユニットから実行ユーザーを特定できないため ${XVC_USER} を使います"
fi

DOTNET_BIN="$(read_installed_dotnet)"
[[ -n "$DOTNET_BIN" && -x "$DOTNET_BIN" ]] || DOTNET_BIN="/opt/dotnet/dotnet"
[[ -x "$DOTNET_BIN" ]] || DOTNET_BIN="$(command -v dotnet || true)"
if [[ -z "$DOTNET_BIN" || ! -x "$DOTNET_BIN" ]]; then
  err "dotnet が見つかりません。先に install.sh を実行してください。"
  exit 1
fi

YTDLP_BIN="$(read_configured_ytdlp "$ENV_FILE")"
[[ -n "$YTDLP_BIN" ]] || YTDLP_BIN="/usr/local/bin/yt-dlp"

PORT="$(read_configured_port "$ENV_FILE")"
HEALTH_URL="http://127.0.0.1:${PORT}/api/health"
STATS_URL="http://127.0.0.1:${PORT}/api/stats"

# sudo 実行時、clone の所有者が実行ユーザー (pi 等) と異なると
# git が "dubious ownership" で止まるため、このリポジトリだけ明示的に許可する。
git_repo() { git -c safe.directory="$REPO_ROOT" -C "$REPO_ROOT" "$@"; }

# JSON から指定キーの整数値を取り出す（jq が無い環境でも動くようにする）。
#   json_number '{"a":1,"b":2}' b  → 2
json_number() {
  local json="$1" key="$2" value
  value="$(sed -n "s/.*\"${key}\"[[:space:]]*:[[:space:]]*\([0-9]\{1,\}\).*/\1/p" <<< "$json" | head -1)"
  echo "${value:-0}"
}

# ── 1. 最新コードを取得 ────────────────────────────────────
BRANCH=""
OLD_REV=""

if [[ $DO_PULL -eq 1 ]]; then
  if [[ ! -d "${REPO_ROOT}/.git" ]]; then
    err "${REPO_ROOT} は git リポジトリではありません。更新するにはクローンが必要です。"
    exit 1
  fi

  CURRENT_BRANCH="$(git_repo rev-parse --abbrev-ref HEAD)"
  BRANCH="${TARGET_BRANCH:-$CURRENT_BRANCH}"

  # --branch 指定時（＝無人実行）は、勝手に checkout せず状態が違えば止まる。
  # 人の作業ブランチを自動で切り替えてしまうほうが危険なため。
  if [[ -n "$TARGET_BRANCH" ]]; then
    if [[ "$CURRENT_BRANCH" != "$TARGET_BRANCH" ]]; then
      err "${REPO_ROOT} は ${CURRENT_BRANCH} をチェックアウトしています（期待: ${TARGET_BRANCH}）。"
      err "自動更新は行いません。切り替えるには: git -C ${REPO_ROOT} checkout ${TARGET_BRANCH}"
      exit 1
    fi

    if [[ -n "$(git_repo status --porcelain)" ]]; then
      err "${REPO_ROOT} にコミットされていない変更があります。自動更新は行いません。"
      git_repo status --short >&2
      exit 1
    fi
  fi

  step "リポジトリ更新"
  info "origin/${BRANCH} を確認中..."
  if ! git_repo fetch --quiet origin "$BRANCH"; then
    err "git fetch に失敗しました（ネットワークまたはリポジトリの問題）"
    exit 1
  fi

  OLD_REV="$(git_repo rev-parse HEAD)"
  NEW_REV="$(git_repo rev-parse "origin/${BRANCH}")"

  if [[ "$OLD_REV" == "$NEW_REV" ]]; then
    if [[ $CHECK_ONLY -eq 1 ]]; then
      echo "更新なし (${BRANCH} は ${OLD_REV:0:7} で最新)"
      exit 1
    fi

    if [[ $IF_CHANGED -eq 1 ]]; then
      # 自動更新の通常経路。ここで終わるので dotnet publish は走らない。
      echo "更新なし (${BRANCH} は ${OLD_REV:0:7} で最新)"
      exit 0
    fi
  elif [[ $CHECK_ONLY -eq 1 ]]; then
    echo "更新あり: ${OLD_REV:0:7} → ${NEW_REV:0:7}"
    git_repo log --oneline "HEAD..origin/${BRANCH}" | head -20
    exit 0
  fi

  # ダウンロード中に再起動すると転送中のファイルが無駄になる。
  # DownloadWorker は再起動後の初回走査で拾い直すが、翌回の timer に回すほうが素直。
  if [[ $DEFER_WHILE_BUSY -eq 1 ]]; then
    STATS_JSON="$(curl -sf --max-time 10 "$STATS_URL" 2>/dev/null || true)"
    if [[ -n "$STATS_JSON" ]]; then
      BUSY=$(( $(json_number "$STATS_JSON" downloadingCount) + $(json_number "$STATS_JSON" processingCount) ))
      if [[ $BUSY -gt 0 ]]; then
        echo "ダウンロード/変換が ${BUSY} 件実行中のため、今回の更新は見送ります"
        exit 0
      fi
    fi
  fi

  info "git pull 実行中..."
  if ! git_repo pull --ff-only origin "$BRANCH"; then
    err "git pull --ff-only に失敗しました。履歴が分岐している可能性があります。"
    err "手動で解決してください: git -C ${REPO_ROOT} status"
    exit 1
  fi
  success "$(git_repo log -1 --oneline)"
fi

# ── 2. 再発行 ──────────────────────────────────────────────
# 直接 $APP_DIR へ発行すると、失敗した時点で稼働中のバイナリが壊れる。
# 一時ディレクトリに出し切ってから入れ替える。
step "アプリケーションの再発行"

PUBLISH_TMP="$(mktemp -d "${APP_DIR}.new.XXXXXX")"
BACKUP_DIR="${APP_DIR}.prev"

# 入れ替え成功後は PUBLISH_TMP を空にするため、この trap は失敗時のみ実際に消す
cleanup_tmp() {
  [[ -n "${PUBLISH_TMP:-}" && -d "$PUBLISH_TMP" ]] && rm -rf "$PUBLISH_TMP"
  return 0
}
trap cleanup_tmp EXIT

info "dotnet publish 実行中..."
if ! "$DOTNET_BIN" publish "${REPO_ROOT}/src/api/XVideoCollector.LocalHost/XVideoCollector.LocalHost.csproj" \
  --configuration Release \
  --runtime linux-arm64 \
  --self-contained false \
  --output "$PUBLISH_TMP" \
  --nologo \
  -v quiet; then
  err "dotnet publish に失敗しました。稼働中のアプリはそのままです。"
  exit 1
fi

# 入れ替え。旧バージョンはヘルスチェックが通るまで .prev に残す。
rm -rf "$BACKUP_DIR"
mv "$APP_DIR" "$BACKUP_DIR"
mv "$PUBLISH_TMP" "$APP_DIR"
PUBLISH_TMP=""

mkdir -p "$SCRIPT_INSTALL_DIR"
chown -R root:"$XVC_USER" "$APP_DIR"
chmod -R g+rX "$APP_DIR"
install -m 750 -o root -g "$XVC_USER" "${SCRIPT_DIR}/backup.sh" "${SCRIPT_INSTALL_DIR}/backup.sh"
success "発行完了"

UNIT_BACKUP_DIR="${BACKUP_DIR}.units"
rm -rf "$UNIT_BACKUP_DIR"

# 失敗時に旧バージョンへ戻す（アプリとユニットは同じコミットから作るので対で戻す）
rollback() {
  err "旧バージョンへ戻しています..."
  rm -rf "$APP_DIR"
  mv "$BACKUP_DIR" "$APP_DIR"
  restore_systemd_units "$UNIT_BACKUP_DIR"
  if systemctl restart "$XVC_SERVICE"; then
    warn "旧バージョン (${OLD_REV:0:7}) で復帰しました。更新は適用されていません。"
  else
    err "旧バージョンでの再起動にも失敗しました。手動で確認してください。"
    stop_failed_service
  fi
}

# ── 3. systemd ユニットの同期 ──────────────────────────────
# 再起動の前に行う。ユニットが変わっていた場合、直後の restart でそのまま反映される。
step "systemd ユニットの同期"

if ! SYNC_RESULT="$(sync_systemd_units "${SCRIPT_DIR}/systemd" "$UNIT_BACKUP_DIR")"; then
  err "systemd ユニットの同期に失敗しました。"
  rollback
  exit 1
fi

if [[ "$SYNC_RESULT" == "新規=0 更新=0" ]]; then
  info "systemd ユニットに変更はありません"
else
  # --quiet でも journald に残るよう echo で出す
  echo "systemd ユニットを更新しました (${SYNC_RESULT})"
fi

# ── 4. 再起動と確認 ────────────────────────────────────────
step "サービス再起動"

if ! systemctl restart "$XVC_SERVICE"; then
  err "サービスの再起動に失敗しました:"
  dump_service_diagnostics "$XVC_SERVICE" "$PORT"
  rollback
  exit 1
fi

HEALTHY=0
for _ in $(seq 1 30); do
  if curl -sf -o /dev/null "$HEALTH_URL"; then
    HEALTHY=1
    break
  fi

  # クラッシュして再起動を繰り返している場合は待たずに打ち切る
  SERVICE_STATE="$(systemctl is-active "$XVC_SERVICE" || true)"
  RESTART_COUNT="$(systemctl show -p NRestarts --value "$XVC_SERVICE" 2>/dev/null || echo 0)"
  if [[ "$SERVICE_STATE" == "failed" || "${RESTART_COUNT:-0}" -gt 0 ]]; then
    err "再起動後にサービスが落ちています (状態: ${SERVICE_STATE}, 再起動回数: ${RESTART_COUNT})"
    dump_service_diagnostics "$XVC_SERVICE" "$PORT"
    rollback
    exit 1
  fi

  sleep 2
done

if [[ $HEALTHY -eq 0 ]]; then
  err "再起動後のヘルスチェックに失敗しました:"
  curl -s "$HEALTH_URL" || true
  echo
  dump_service_diagnostics "$XVC_SERVICE" "$PORT"
  rollback
  exit 1
fi

rm -rf "$BACKUP_DIR" "$UNIT_BACKUP_DIR"

# systemd ユニットは同期済み。一方、install.sh 自体と env の雛形は
# このスクリプトでは反映できない（依存の導入や設定生成を伴うため）。
# 差分があった場合だけ気付けるよう警告を残す。
if [[ -n "$OLD_REV" ]]; then
  CHANGED_INSTALLER="$(git_repo diff --name-only "$OLD_REV" HEAD -- \
    scripts/raspi/install.sh scripts/raspi/xvideocollector.env.example 2>/dev/null || true)"
  if [[ -n "$CHANGED_INSTALLER" ]]; then
    warn "インストーラまたは設定雛形が更新されています。内容によっては install.sh の再実行が必要です:"
    warn "  sudo bash ${REPO_ROOT}/scripts/raspi/install.sh"
    while IFS= read -r f; do warn "    - ${f}"; done <<< "$CHANGED_INSTALLER"
  fi
fi

if [[ -n "$OLD_REV" ]]; then
  success "更新完了: ${OLD_REV:0:7} → $(git_repo rev-parse --short HEAD)（ヘルスチェック OK）"
else
  success "更新完了（ヘルスチェック OK）"
fi
exit 0
