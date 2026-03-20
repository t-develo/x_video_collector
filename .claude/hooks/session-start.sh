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
