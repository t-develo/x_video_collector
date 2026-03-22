#!/bin/bash
set -euo pipefail

# Only run in remote (Claude Code on the web) environments
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# ── .NET SDK のインストール ───────────────────────────────────────────
if ! command -v dotnet &>/dev/null; then
  echo "[session-start] dotnet not found. Installing .NET 10 SDK..."
  apt-get update -qq
  apt-get install -y -qq dotnet-sdk-10.0
  echo "[session-start] .NET $(dotnet --version) installed."
else
  echo "[session-start] dotnet $(dotnet --version) already installed."
fi

# ── NuGet パッケージの復元 ────────────────────────────────────────────
echo "[session-start] Restoring NuGet packages..."
dotnet restore "${CLAUDE_PROJECT_DIR}/XVideoCollector.slnx"
echo "[session-start] Done."

# ── セッション履歴の復元 ─────────────────────────────────────────────
SESSIONS_DIR="${CLAUDE_PROJECT_DIR}/.claude/sessions"

if [ -d "${SESSIONS_DIR}" ]; then
  # 最新のセッションファイルを探す（ファイル名の日付順でソート）
  LATEST_SESSION=$(ls -1 "${SESSIONS_DIR}"/*.md 2>/dev/null | sort | tail -1 || true)

  if [ -n "${LATEST_SESSION}" ]; then
    SESSION_DATE=$(basename "${LATEST_SESSION}" | cut -c1-10)
    echo ""
    echo "╔══════════════════════════════════════════════════════════════╗"
    echo "║  📋 前回のセッション記録が見つかりました: ${SESSION_DATE}        ║"
    echo "╚══════════════════════════════════════════════════════════════╝"
    echo ""
    echo "--- セッション概要 ---"
    # 最初の30行を表示（ファイルが大きすぎる場合の対策）
    head -n 30 "${LATEST_SESSION}"
    echo ""
    echo "[session-start] /resume-session で完全な内容を復元できます。"
  else
    echo "[session-start] セッション記録なし。/save-session でセッションを保存できます。"
  fi
else
  echo "[session-start] セッション記録なし。/save-session でセッションを保存できます。"
fi
