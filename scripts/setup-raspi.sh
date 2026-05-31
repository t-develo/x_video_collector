#!/usr/bin/env bash
# X Video Collector — Raspberry Pi セットアップスクリプト
#
# 買ってきたばかりの Raspberry Pi （素の Raspberry Pi OS / Debian）上で
# アプリを動かすための開発/実行スタックを一括導入する。
#
# やること:
#   - ツールの確認（OS / アーキテクチャ / sudo）
#   - apt によるベース依存パッケージの導入（リポジトリに無い場合のフォールバック付き）
#   - .NET 10 SDK（公式 dotnet-install.sh, ARM 対応）
#   - Node.js 22（NodeSource → 失敗時は公式 tarball にフォールバック）
#   - yt-dlp / ffmpeg / ffprobe（ARM 対応バイナリ）
#   - Azure Functions Core Tools v4 / Azurite / dotnet-ef / npm install
#   - local.settings.json（SQLite, SKIP_AUTH）生成
#   - PATH 永続化 & ビルド確認
#
# 対応アーキ: arm64 (aarch64) / armhf (armv7l) / amd64 (x86_64)
# 非対応: armv6 (Pi Zero / Pi 1) — .NET が非対応
#
# 使い方:
#   bash scripts/setup-raspi.sh
#
# 注意: メモリの少ない Pi では func / azurite / dotnet を同時に動かすと重い。
#       可能なら 64bit OS（arm64）+ メモリ 2GB 以上を推奨。

set -euo pipefail

# ── ログ関数 ───────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'

# ログは stderr に出す（command substitution で値を取り込む処理を汚さないため）
info()    { echo -e "${BLUE}[INFO]${NC}  $*" >&2; }
success() { echo -e "${GREEN}[ OK ]${NC}  $*" >&2; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*" >&2; }
error()   { echo -e "${RED}[FAIL]${NC} $*" >&2; }
step()    { echo -e "\n${BOLD}━━━ $* ━━━${NC}" >&2; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
FUNCTIONS_DIR="${REPO_ROOT}/src/api/XVideoCollector.Functions"

LOCAL_BIN="$HOME/.local/bin"
NPM_GLOBAL="$HOME/.npm-global"
DOTNET_ROOT="$HOME/.dotnet"

mkdir -p "$LOCAL_BIN" "$NPM_GLOBAL"

# このスクリプト内で使う PATH を先に通しておく
export PATH="$NPM_GLOBAL/bin:$LOCAL_BIN:$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
export DOTNET_ROOT

# ════════════════════════════════════════════════════════════
# 1. プリフライト（ツールの確認）
# ════════════════════════════════════════════════════════════
step "プリフライト（環境チェック）"

# --- OS 判定（Debian 系か） ---
IS_DEBIAN=0
OS_NAME="unknown"
if [[ -r /etc/os-release ]]; then
  # shellcheck disable=SC1091
  . /etc/os-release
  OS_NAME="${PRETTY_NAME:-${NAME:-unknown}}"
  case "${ID:-}${ID_LIKE:-}" in
    *debian*|*raspbian*|*ubuntu*) IS_DEBIAN=1 ;;
  esac
fi
info "OS: ${OS_NAME}"
if [[ "$IS_DEBIAN" -ne 1 ]]; then
  warn "Debian 系 OS が検出できませんでした。apt 前提の処理が失敗する可能性があります。"
  read -r -p "続行しますか? [y/N] " _ans
  [[ "${_ans:-N}" =~ ^[Yy]$ ]] || { error "中止しました。"; exit 1; }
fi

# --- アーキテクチャ判定 ---
# DOTNET_ARCH      : dotnet-install.sh の --architecture 値
# NODE_ARCH        : nodejs.org tarball のアーキ識別子
# YTDLP_ASSET      : yt-dlp リリースのアセット名
# FFMPEG_ARCH      : johnvansickle 静的ビルドのアーキ識別子
MACHINE="$(uname -m)"
case "$MACHINE" in
  aarch64|arm64)
    DOTNET_ARCH="arm64"; NODE_ARCH="arm64"
    YTDLP_ASSET="yt-dlp_linux_aarch64"; FFMPEG_ARCH="arm64" ;;
  armv7l|armhf)
    DOTNET_ARCH="arm"; NODE_ARCH="armv7l"
    YTDLP_ASSET="yt-dlp_linux_armv7l"; FFMPEG_ARCH="armhf" ;;
  x86_64|amd64)
    DOTNET_ARCH="x64"; NODE_ARCH="x64"
    YTDLP_ASSET="yt-dlp"; FFMPEG_ARCH="amd64" ;;
  armv6l)
    error "armv6 (Pi Zero / Pi 1) は .NET が非対応のため、このアプリは動作しません。"
    error "arm64 対応の Pi（Pi 3 以降）+ 64bit OS をご利用ください。"
    exit 1 ;;
  *)
    error "未知のアーキテクチャ: ${MACHINE}。手動セットアップが必要です。"
    exit 1 ;;
esac
info "アーキテクチャ: ${MACHINE} → dotnet=${DOTNET_ARCH}, node=${NODE_ARCH}, ffmpeg=${FFMPEG_ARCH}"

# --- 32bit OS への注意 ---
if [[ "$DOTNET_ARCH" == "arm" ]]; then
  warn "32bit OS (armhf) を検出。.NET の arm(32bit) は動作しますが、64bit OS (arm64) を強く推奨します。"
fi

# --- sudo 判定 ---
SUDO=""
if [[ "$(id -u)" -eq 0 ]]; then
  SUDO=""
  info "root で実行中。"
elif command -v sudo &>/dev/null; then
  SUDO="sudo"
  info "sudo を使用してシステムパッケージを導入します。"
else
  warn "sudo が見つかりません。apt によるシステムパッケージ導入はスキップされ、フォールバックに依存します。"
fi

# ════════════════════════════════════════════════════════════
# 2. apt ヘルパー（リポジトリ欠如・失敗に強い）
# ════════════════════════════════════════════════════════════

APT_AVAILABLE=0
command -v apt-get &>/dev/null && APT_AVAILABLE=1

# apt-get update（一部リポジトリ失敗で全体を止めない）
apt_update_safe() {
  [[ "$APT_AVAILABLE" -eq 1 ]] || return 0
  info "apt-get update を実行中..."
  if $SUDO apt-get update -y 2>/dev/null; then
    success "apt-get update 完了"
  else
    warn "apt-get update で一部リポジトリが失敗しました（続行します）。"
  fi
}

# 単一パッケージが apt の候補に存在するか
apt_has_candidate() {
  local pkg="$1"
  [[ "$APT_AVAILABLE" -eq 1 ]] || return 1
  local cand
  cand="$(apt-cache policy "$pkg" 2>/dev/null | awk '/Candidate:/{print $2}')"
  [[ -n "$cand" && "$cand" != "(none)" ]]
}

# apt で best-effort インストール。
# 候補が無い／失敗したパッケージ名を標準出力に列挙し、1つでもあれば非ゼロを返す。
apt_install() {
  if [[ "$APT_AVAILABLE" -ne 1 ]]; then
    printf '%s\n' "$@"
    return 1
  fi
  local missing=()
  for pkg in "$@"; do
    if apt_has_candidate "$pkg"; then
      if ! $SUDO apt-get install -y "$pkg" 2>/dev/null; then
        warn "apt: ${pkg} のインストールに失敗しました。"
        missing+=("$pkg")
      fi
    else
      warn "apt: ${pkg} はリポジトリに候補がありません。"
      missing+=("$pkg")
    fi
  done
  if [[ "${#missing[@]}" -gt 0 ]]; then
    printf '%s\n' "${missing[@]}"
    return 1
  fi
  return 0
}

# ════════════════════════════════════════════════════════════
# 3. ベース依存パッケージ
# ════════════════════════════════════════════════════════════
step "ベース依存パッケージ (apt)"
apt_update_safe
BASE_PKGS=(curl ca-certificates jq unzip xz-utils tar git python3 python3-pip build-essential libicu-dev)
if MISSING="$(apt_install "${BASE_PKGS[@]}")"; then
  success "ベース依存パッケージ導入完了"
else
  warn "一部のベースパッケージが導入できませんでした: ${MISSING//$'\n'/ }"
  # libicu が無いと .NET が globalization で落ちるため invariant モードを案内
  if grep -q "libicu" <<<"$MISSING"; then
    warn "libicu が無いため、.NET 実行時に DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 が必要になる場合があります。"
  fi
  if ! command -v curl &>/dev/null; then
    error "curl が導入できませんでした。以降のダウンロードができないため中止します。"
    exit 1
  fi
fi

# ════════════════════════════════════════════════════════════
# 4. .NET 10 SDK
# ════════════════════════════════════════════════════════════
step ".NET 10 SDK"
if command -v dotnet &>/dev/null && dotnet --list-sdks 2>/dev/null | grep -q "^10\."; then
  success "dotnet SDK 10.x — 既にインストール済み ($(dotnet --version))"
else
  info "公式 dotnet-install.sh で .NET 10 SDK (${DOTNET_ARCH}) を ${DOTNET_ROOT} へ導入中..."
  DOTNET_INSTALL_TMP="$(mktemp)"
  if curl -fsSL "https://dot.net/v1/dotnet-install.sh" -o "$DOTNET_INSTALL_TMP"; then
    chmod +x "$DOTNET_INSTALL_TMP"
    "$DOTNET_INSTALL_TMP" --channel 10.0 --architecture "$DOTNET_ARCH" --install-dir "$DOTNET_ROOT"
    rm -f "$DOTNET_INSTALL_TMP"
    if command -v dotnet &>/dev/null; then
      success ".NET SDK $(dotnet --version) インストール完了"
    else
      error ".NET 導入後に dotnet が見つかりません。PATH を確認してください。"
      exit 1
    fi
  else
    error "dotnet-install.sh のダウンロードに失敗しました。ネットワークを確認してください。"
    exit 1
  fi
fi

# ════════════════════════════════════════════════════════════
# 5. Node.js 22
# ════════════════════════════════════════════════════════════
step "Node.js 22"
node_major() { node -v 2>/dev/null | sed 's/^v//; s/\..*//'; }
if command -v node &>/dev/null && [[ "$(node_major)" -ge 22 ]] 2>/dev/null; then
  success "Node.js $(node -v) — 既にインストール済み"
else
  NODE_OK=0
  # 第一候補: NodeSource (apt リポジトリ追加)
  if [[ "$APT_AVAILABLE" -eq 1 && -n "$SUDO" || "$(id -u)" -eq 0 ]] && command -v curl &>/dev/null; then
    info "NodeSource 経由で Node.js 22 を導入中..."
    if curl -fsSL "https://deb.nodesource.com/setup_22.x" -o /tmp/nodesource_setup.sh \
       && $SUDO bash /tmp/nodesource_setup.sh 2>/dev/null \
       && apt_install nodejs >/dev/null; then
      command -v node &>/dev/null && [[ "$(node_major)" -ge 22 ]] && NODE_OK=1
    fi
    rm -f /tmp/nodesource_setup.sh
    [[ "$NODE_OK" -eq 1 ]] && success "Node.js $(node -v) (NodeSource) インストール完了" \
                           || warn "NodeSource 経由の導入に失敗。公式 tarball にフォールバックします。"
  fi
  # フォールバック: nodejs.org 公式 tarball を ~/.local へ展開
  if [[ "$NODE_OK" -ne 1 ]]; then
    info "nodejs.org 公式 tarball (${NODE_ARCH}) を取得中..."
    NODE_VER="$(curl -fsSL https://nodejs.org/dist/latest-v22.x/ 2>/dev/null \
                | grep -oE 'node-v22\.[0-9.]+-linux-'"${NODE_ARCH}"'\.tar\.xz' | head -1)"
    if [[ -z "$NODE_VER" ]]; then
      error "Node.js 22 の tarball 名を取得できませんでした。"
      exit 1
    fi
    NODE_TMP="$(mktemp -d)"
    if curl -fsSL "https://nodejs.org/dist/latest-v22.x/${NODE_VER}" -o "${NODE_TMP}/node.tar.xz"; then
      mkdir -p "$HOME/.local"
      tar xf "${NODE_TMP}/node.tar.xz" -C "$HOME/.local" --strip-components=1
      rm -rf "$NODE_TMP"
      if command -v node &>/dev/null && [[ "$(node_major)" -ge 22 ]]; then
        success "Node.js $(node -v) (公式 tarball) インストール完了"
      else
        error "Node.js 展開後に node が見つかりません。"
        exit 1
      fi
    else
      error "Node.js tarball のダウンロードに失敗しました。"
      exit 1
    fi
  fi
fi

# ════════════════════════════════════════════════════════════
# 6. ffmpeg / ffprobe（ARM 対応）
# ════════════════════════════════════════════════════════════
step "ffmpeg / ffprobe"
if command -v ffmpeg &>/dev/null && command -v ffprobe &>/dev/null; then
  success "ffmpeg / ffprobe — 既にインストール済み"
else
  FFMPEG_OK=0
  # 第一候補: apt (Raspberry Pi OS / Debian は ARM 版 ffmpeg を提供)
  if apt_install ffmpeg >/dev/null 2>&1; then
    command -v ffmpeg &>/dev/null && command -v ffprobe &>/dev/null && FFMPEG_OK=1
    [[ "$FFMPEG_OK" -eq 1 ]] && success "ffmpeg (apt) インストール完了"
  fi
  # フォールバック: johnvansickle のアーキ別静的ビルド
  if [[ "$FFMPEG_OK" -ne 1 ]]; then
    info "ffmpeg 静的ビルド (${FFMPEG_ARCH}) をダウンロード中..."
    FFMPEG_TMP="$(mktemp -d)"
    FFMPEG_URL="https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-${FFMPEG_ARCH}-static.tar.xz"
    if curl -fsSL "$FFMPEG_URL" -o "${FFMPEG_TMP}/ffmpeg.tar.xz"; then
      tar xf "${FFMPEG_TMP}/ffmpeg.tar.xz" -C "${FFMPEG_TMP}" --strip-components=1
      cp "${FFMPEG_TMP}/ffmpeg"  "$LOCAL_BIN/"
      cp "${FFMPEG_TMP}/ffprobe" "$LOCAL_BIN/" 2>/dev/null || true
      chmod +x "$LOCAL_BIN/ffmpeg" "$LOCAL_BIN/ffprobe" 2>/dev/null || true
      rm -rf "$FFMPEG_TMP"
      if command -v ffmpeg &>/dev/null; then
        success "ffmpeg (静的ビルド) インストール完了"
      else
        error "ffmpeg 配置後に見つかりません。PATH を確認してください。"
        exit 1
      fi
    else
      rm -rf "$FFMPEG_TMP"
      error "ffmpeg のダウンロードに失敗しました。"
      exit 1
    fi
  fi
fi

# ════════════════════════════════════════════════════════════
# 7. yt-dlp（ARM 対応）
# ════════════════════════════════════════════════════════════
step "yt-dlp"
if command -v yt-dlp &>/dev/null; then
  success "yt-dlp $(yt-dlp --version 2>/dev/null) — 既にインストール済み"
else
  YTDLP_OK=0
  # 第一候補: アーキ別スタンドアロンバイナリ（PyInstaller 製, Python 不要）
  info "yt-dlp (${YTDLP_ASSET}) をダウンロード中..."
  if curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/latest/download/${YTDLP_ASSET}" \
       -o "$LOCAL_BIN/yt-dlp"; then
    chmod +x "$LOCAL_BIN/yt-dlp"
    if "$LOCAL_BIN/yt-dlp" --version &>/dev/null; then
      YTDLP_OK=1
      success "yt-dlp $("$LOCAL_BIN/yt-dlp" --version) インストール完了"
    else
      warn "yt-dlp バイナリが動作しません。pip にフォールバックします。"
      rm -f "$LOCAL_BIN/yt-dlp"
    fi
  else
    warn "yt-dlp バイナリの取得に失敗。pip にフォールバックします。"
  fi
  # フォールバック: pip3 install --user
  if [[ "$YTDLP_OK" -ne 1 ]]; then
    if command -v pip3 &>/dev/null; then
      info "pip3 install --user yt-dlp を実行中..."
      pip3 install --user --upgrade yt-dlp || pip3 install --user --break-system-packages --upgrade yt-dlp
      if command -v yt-dlp &>/dev/null; then
        success "yt-dlp $(yt-dlp --version) (pip) インストール完了"
      else
        error "yt-dlp の導入に失敗しました。"
        exit 1
      fi
    else
      error "yt-dlp バイナリ取得に失敗し、pip3 も利用できません。"
      exit 1
    fi
  fi
fi

# ════════════════════════════════════════════════════════════
# 8. プロジェクト固有ツール
# ════════════════════════════════════════════════════════════
step "npm グローバルプレフィックス設定"
npm config set prefix "$NPM_GLOBAL"
success "npm prefix → $NPM_GLOBAL"

step "Azure Functions Core Tools v4"
if command -v func &>/dev/null && func --version 2>/dev/null | grep -qE "^4\."; then
  success "func $(func --version) — 既にインストール済み"
else
  info "Azure Functions Core Tools v4 をインストール中... (Pi では数分かかる場合があります)"
  npm install -g azure-functions-core-tools@4 --unsafe-perm true
  success "func $(func --version) インストール完了"
fi

step "Azurite (Azure Storage エミュレーター)"
if command -v azurite &>/dev/null; then
  success "azurite — 既にインストール済み"
else
  info "Azurite をインストール中..."
  npm install -g azurite
  success "Azurite インストール完了"
fi

step "dotnet-ef (EF Core CLI ツール)"
if dotnet tool list -g 2>/dev/null | grep -q "dotnet-ef"; then
  success "dotnet-ef — 既にインストール済み"
else
  info "dotnet-ef をインストール中..."
  dotnet tool install -g dotnet-ef
  success "dotnet-ef インストール完了"
fi

step "npm パッケージ (プロジェクトルート)"
cd "${REPO_ROOT}"
if [[ -d node_modules ]]; then
  success "node_modules — 既にインストール済み"
else
  info "npm install 中..."
  npm install
  success "npm install 完了"
fi

# ════════════════════════════════════════════════════════════
# 9. local.settings.json 生成（実際の導入先パスを解決）
# ════════════════════════════════════════════════════════════
step "local.settings.json (Azure Functions ローカル設定)"
LOCAL_SETTINGS="${FUNCTIONS_DIR}/local.settings.json"

# 実際に解決される実行ファイルパスを使う（apt なら /usr/bin、static なら ~/.local/bin）
YTDLP_PATH="$(command -v yt-dlp || echo "${LOCAL_BIN}/yt-dlp")"
FFMPEG_PATH="$(command -v ffmpeg || echo "${LOCAL_BIN}/ffmpeg")"
FFPROBE_PATH="$(command -v ffprobe || echo "${LOCAL_BIN}/ffprobe")"

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
    "YtDlp__ExecutablePath": "${YTDLP_PATH}",
    "YtDlp__FfmpegPath": "${FFMPEG_PATH}",
    "YtDlp__FfprobePath": "${FFPROBE_PATH}",
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

# ════════════════════════════════════════════════════════════
# 10. PATH 永続化（~/.bashrc に重複ガード付きで追記）
# ════════════════════════════════════════════════════════════
step "PATH 永続化"
PROFILE="$HOME/.bashrc"
PATH_LINE='export PATH="$HOME/.npm-global/bin:$HOME/.local/bin:$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"'
DOTNET_LINE='export DOTNET_ROOT="$HOME/.dotnet"'
touch "$PROFILE"
if grep -qF "$PATH_LINE" "$PROFILE"; then
  success "PATH は既に ${PROFILE} に設定済み"
else
  {
    echo ""
    echo "# X Video Collector — Raspberry Pi setup による PATH 設定"
    echo "$DOTNET_LINE"
    echo "$PATH_LINE"
  } >> "$PROFILE"
  success "PATH を ${PROFILE} に追記しました"
fi

# ════════════════════════════════════════════════════════════
# 11. ビルド確認
# ════════════════════════════════════════════════════════════
step ".NET ビルド確認"
cd "${REPO_ROOT}"
dotnet build src/api/XVideoCollector.Functions/XVideoCollector.Functions.csproj \
  --configuration Debug -v quiet
success ".NET ビルド成功"

# ════════════════════════════════════════════════════════════
# 完了サマリ
# ════════════════════════════════════════════════════════════
step "セットアップ完了"
echo -e "${GREEN}"
echo "  ✔ .NET SDK        ($(dotnet --version))"
echo "  ✔ Node.js         ($(node -v))"
echo "  ✔ Azure Functions Core Tools v4 ($(func --version 2>/dev/null))"
echo "  ✔ Azurite"
echo "  ✔ dotnet-ef"
echo "  ✔ yt-dlp          (${YTDLP_PATH})"
echo "  ✔ ffmpeg          (${FFMPEG_PATH})"
echo "  ✔ ffprobe         (${FFPROBE_PATH})"
echo "  ✔ local.settings.json (SQLite DB)"
echo "  ✔ .NET ビルド OK"
echo -e "${NC}"

echo -e "新しいシェルを開くか、以下を実行して PATH を反映してください:"
echo -e "  ${BOLD}source ~/.bashrc${NC}"
echo ""
echo -e "次のステップ: ${BOLD}bash scripts/start-dev.sh${NC}"
echo -e "  → ブラウザで http://localhost:3000 を開く"
