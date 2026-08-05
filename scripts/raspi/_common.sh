#!/usr/bin/env bash
# X Video Collector — ラズパイ用スクリプトの共通処理
#
# install.sh / update.sh から source して使う:
#   source "${SCRIPT_DIR}/_common.sh"

XVC_SERVICE="xvideocollector.service"

# ── ポート ─────────────────────────────────────────────────

# env ファイルの ASPNETCORE_URLS から待ち受けポートを取り出す。
# 取得できない場合は 8080 を返す。
#   read_configured_port /etc/xvideocollector/xvideocollector.env
read_configured_port() {
  local env_file="$1"
  local port=""

  if [[ -f "$env_file" ]]; then
    # ASPNETCORE_URLS=http://0.0.0.0:8080 や http://+:8080 等、ホスト部を問わず拾う
    port="$(sed -n 's|^ASPNETCORE_URLS=.*://[^:/]*:\([0-9]\{1,5\}\).*|\1|p' "$env_file" | head -1)"
  fi

  echo "${port:-8080}"
}

# 指定ポートを LISTEN しているプロセスの説明を返す（未使用なら空文字）。
#   port_listener 8080  → users:(("dotnet",pid=1234,fd=200))
port_listener() {
  local port="$1"

  if command -v ss &>/dev/null; then
    # ヘッダ行には LISTEN が含まれないため、grep で実データ行だけを取り出す
    ss -ltnp "sport = :${port}" 2>/dev/null | grep -w LISTEN | awk '{print $NF}' | head -1
    return 0
  fi

  # ss が無い環境では接続可否だけで判定する（プロセス名までは分からない）
  if (exec 3<>"/dev/tcp/127.0.0.1/${port}") 2>/dev/null; then
    echo "(プロセス不明: 127.0.0.1:${port} が応答しています)"
  fi
}

# ── サービス診断 ───────────────────────────────────────────

# 起動失敗時に原因が分かるよう status と直近のログを出力する。
#   dump_service_diagnostics [ユニット名]
dump_service_diagnostics() {
  local unit="${1:-$XVC_SERVICE}"

  echo
  echo "──── systemctl status ${unit} ────"
  systemctl status "$unit" --no-pager -l || true

  echo
  echo "──── journalctl --unit=${unit} -n 40 ────"
  journalctl --unit="$unit" -n 40 --no-pager || true
  echo
}
