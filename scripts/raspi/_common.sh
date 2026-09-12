#!/usr/bin/env bash
# X Video Collector — ラズパイ用スクリプトの共通処理
#
# install.sh / update.sh から source して使う:
#   source "${SCRIPT_DIR}/_common.sh"

XVC_SERVICE="xvideocollector.service"
XVC_ENV_FILE="/etc/xvideocollector/xvideocollector.env"
XVC_LOCK_FILE="/run/xvideocollector-update.lock"
# systemd ユニットの配置先。テストで差し替えられるよう変数にしている。
XVC_UNIT_DIR="${XVC_UNIT_DIR:-/etc/systemd/system}"

# ── 排他制御 ───────────────────────────────────────────────

# 発行先 (/opt/xvideocollector) を書き換えるスクリプトを 1 つだけに絞る。
# install.sh と自動更新タイマーが同時に走ると発行先を奪い合うため、両方で取得する。
# fd 9 を開いたまま保持し、スクリプト終了時に自動で解放させる。
# 取得できた場合は 0、他が実行中なら 1 を返す（呼び出し側で扱いを決める）。
acquire_update_lock() {
  if ! command -v flock &>/dev/null; then
    return 0
  fi

  exec 9>"$XVC_LOCK_FILE"
  flock -n 9
}

# ── 導入済み構成の読み出し ─────────────────────────────────
#
# install.sh の再実行や update.sh からのユニット再描画では、
# 導入時に指定された値（実行ユーザー・各種パス）を復元する必要がある。
# 値は env ファイルと導入済みユニットの両方に残っているのでそこから拾う。

# env ファイルから指定キーの値を取り出す（見つからなければ空文字）。
#   read_env_value /etc/xvideocollector/xvideocollector.env LocalStorage__RootPath
read_env_value() {
  local env_file="$1" key="$2"

  [[ -f "$env_file" ]] || return 0

  # コメント行 (#) は拾わない。値に = が含まれていても最初の = で切る。
  sed -n "s|^${key}=||p" "$env_file" | head -1
}

# メディア保存先 (LocalStorage__RootPath)。未設定なら空文字。
read_configured_media_path() {
  read_env_value "${1:-$XVC_ENV_FILE}" LocalStorage__RootPath
}

# yt-dlp の実行ファイルパス (YtDlp__ExecutablePath)。未設定なら空文字。
read_configured_ytdlp() {
  read_env_value "${1:-$XVC_ENV_FILE}" YtDlp__ExecutablePath
}

# 導入済みユニットから指定ディレクティブの値を取り出す（未導入なら空文字）。
#   read_installed_unit_value User
read_installed_unit_value() {
  local key="$1" unit_file="${XVC_UNIT_DIR}/${XVC_SERVICE}"

  [[ -f "$unit_file" ]] || return 0

  sed -n "s|^${key}=||p" "$unit_file" | head -1
}

# サービス実行ユーザー。未導入なら空文字。
read_installed_user() {
  read_installed_unit_value User
}

# dotnet の実行ファイルパス。ExecStart の第1トークンとして入っている。
# 未導入なら空文字。
read_installed_dotnet() {
  read_installed_unit_value ExecStart | awk '{print $1}'
}

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

# env ファイルの ASPNETCORE_URLS を指定ポートに書き換える（行が無ければ追加する）。
# install.sh は既存の env を上書きしないため、--port の指定はここで反映する。
#   set_configured_port /etc/xvideocollector/xvideocollector.env 58180
set_configured_port() {
  local env_file="$1" port="$2" value

  if ! grep -q '^ASPNETCORE_URLS=' "$env_file"; then
    printf 'ASPNETCORE_URLS=http://0.0.0.0:%s\n' "$port" >> "$env_file"
    return 0
  fi

  value="$(sed -n 's|^ASPNETCORE_URLS=||p' "$env_file" | head -1)"

  if [[ "$value" =~ ^[^:]+://[^:/]+:[0-9]{1,5}$ ]]; then
    # ホスト部 (0.0.0.0 / + / localhost) は保ったままポート番号だけ差し替える
    sed -i -E "s|^(ASPNETCORE_URLS=[^:]+://[^:/]*):[0-9]{1,5}.*|\1:${port}|" "$env_file"
  else
    # 複数アドレス指定やポート無しの指定は、単一アドレスへ置き換える
    sed -i -E "s|^ASPNETCORE_URLS=.*|ASPNETCORE_URLS=http://0.0.0.0:${port}|" "$env_file"
  fi
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

# 指定ポートを LISTEN しているプロセスの PID を列挙する（1 行 1 PID）。
#   port_listener_pids 8080  → 1234
port_listener_pids() {
  local port="$1"

  if command -v ss &>/dev/null; then
    ss -ltnp "sport = :${port}" 2>/dev/null | grep -w LISTEN \
      | grep -o 'pid=[0-9]\{1,\}' | cut -d= -f2 | sort -u
    return 0
  fi

  if command -v lsof &>/dev/null; then
    lsof -tiTCP:"${port}" -sTCP:LISTEN 2>/dev/null | sort -u
  fi

  # ss も lsof も無い環境で非ゼロを返すと、呼び出し側の set -e で落ちてしまう
  return 0
}

# PID が属する systemd ユニット名を返す（systemd 管理外なら空文字）。
#   pid_unit 1234  → xvideocollector.service
pid_unit() {
  local pid="$1"

  [[ -r "/proc/${pid}/cgroup" ]] || return 0
  sed -n 's|.*/\([^/]\{1,\}\.service\).*|\1|p' "/proc/${pid}/cgroup" | head -1
}

# PID の概要（PID / 実行ユーザー / コマンドライン）を 1 行で返す。
pid_summary() {
  local pid="$1"
  ps -o pid=,user=,args= -p "$pid" 2>/dev/null | sed 's|^ *||'
}

# ポート競合の内容（誰が占有しているか）と対処方法を標準エラーへ出力する。
#   report_port_conflict 8080
report_port_conflict() {
  local port="$1"
  local listener pids pid unit

  err "ポート ${port} は既に使用されています"

  pids="$(port_listener_pids "$port")"
  if [[ -n "$pids" ]]; then
    err "占有プロセス:"
    while read -r pid; do
      [[ -n "$pid" ]] || continue
      err "  $(pid_summary "$pid")"

      unit="$(pid_unit "$pid")"
      if [[ -n "$unit" ]]; then
        err "    → systemd ユニット ${unit}:  sudo systemctl stop ${unit}"
      else
        err "    → systemd 管理外のプロセス（手動起動と思われる）:  sudo kill ${pid}"
      fi
    done <<< "$pids"
  else
    # PID まで分からない環境（非 root / ss も lsof も無い）では分かる情報だけ出す
    listener="$(port_listener "$port")"
    err "占有プロセスを特定できませんでした${listener:+: ${listener}}"
    err "root で実行するか、ss / lsof を導入すると占有プロセスを特定できます"
  fi

  err "対処:"
  err "  1. 上記の占有プロセスを停止してからやり直す"
  err "  2. 別のポートを使う   sudo bash scripts/raspi/install.sh --port <空きポート>"
  err "     （導入済みなら /etc/xvideocollector/xvideocollector.env の ASPNETCORE_URLS を変更して再起動）"
}

# ポートが空いていれば 0、塞がっていれば対処方法を出力して 1 を返す。
#   ensure_port_free 8080 || exit 1
ensure_port_free() {
  local port="$1"

  if [[ -n "$(port_listener "$port")" ]]; then
    report_port_conflict "$port"
    return 1
  fi

  return 0
}

# ── サービス診断 ───────────────────────────────────────────

# 起動失敗時に原因が分かるよう status と直近のログを出力する。
# ログに「address already in use」が出ていた場合は、占有プロセスまで特定して示す。
#   dump_service_diagnostics [ユニット名] [ポート]
dump_service_diagnostics() {
  local unit="${1:-$XVC_SERVICE}"
  local port="${2:-}"
  local journal

  echo
  echo "──── systemctl status ${unit} ────"
  systemctl status "$unit" --no-pager -l || true

  journal="$(journalctl --unit="$unit" -n 40 --no-pager 2>/dev/null || true)"
  echo
  echo "──── journalctl --unit=${unit} -n 40 ────"
  echo "$journal"
  echo

  # ポート競合はログのスタックトレースだけでは「誰が使っているか」が分からないため補足する
  if grep -qi 'address already in use' <<< "$journal"; then
    [[ -n "$port" ]] || port="$(read_configured_port "${XVC_ENV_FILE}")"
    echo "──── ポート競合の詳細 ────"
    report_port_conflict "$port"
    echo
  fi
}

# ── systemd ユニット ───────────────────────────────────────

# ユニットテンプレートのプレースホルダを実際の値へ置き換えて書き出す。
# 呼び出し側が次のグローバルを定義しておくこと:
#   XVC_USER / APP_DIR / DATA_DIR / CONFIG_DIR / SCRIPT_INSTALL_DIR
#   DOTNET_BIN / YTDLP_BIN / REPO_ROOT
#   render_unit scripts/raspi/systemd/xvideocollector.service /etc/systemd/system/xvideocollector.service
render_unit() {
  local src="$1" dest="$2"
  sed \
    -e "s|__XVC_USER__|${XVC_USER}|g" \
    -e "s|__XVC_APP_DIR__|${APP_DIR}|g" \
    -e "s|__XVC_DATA_DIR__|${DATA_DIR}|g" \
    -e "s|__XVC_CONFIG_DIR__|${CONFIG_DIR}|g" \
    -e "s|__XVC_SCRIPT_DIR__|${SCRIPT_INSTALL_DIR}|g" \
    -e "s|__XVC_DOTNET__|${DOTNET_BIN}|g" \
    -e "s|__XVC_YTDLP__|${YTDLP_BIN}|g" \
    -e "s|__XVC_REPO_DIR__|${REPO_ROOT}|g" \
    "$src" > "$dest"
}

# systemctl の呼び出しはテストで差し替えられるよう関数にしておく。
xvc_daemon_reload() { systemctl daemon-reload; }
xvc_enable_unit()   { systemctl enable --now "$1"; }

# テンプレートを描画し、導入済みのユニットと内容比較して差分があるものだけ入れ替える。
#
# 第1引数: テンプレートのあるディレクトリ (scripts/raspi/systemd)
# 第2引数: 置換前のユニットを退避するディレクトリ（ロールバック用・省略可）
#
# 標準出力に "新規=<n> 更新=<n>" を返し、変更が無ければ両方 0 になる。
#
# 新規に配置したタイマーだけ enable する。既に存在するタイマーは触らない
# （利用者が意図的に disable したものを勝手に有効化しないため）。
#
# 実行中の xvideocollector-update.service 自身のファイルも書き換わりうるが、
# systemd はユニットを起動時に読み込み済みなので走っているインスタンスには
# 影響しない。新しい内容は次回の発火から効く。
sync_systemd_units() {
  local template_dir="$1" backup_dir="${2:-}"
  local tmp_dir name rendered installed
  local -a new_units=() changed_units=()

  tmp_dir="$(mktemp -d)"

  for src in "${template_dir}"/*; do
    [[ -f "$src" ]] || continue
    name="$(basename "$src")"
    rendered="${tmp_dir}/${name}"
    installed="${XVC_UNIT_DIR}/${name}"

    render_unit "$src" "$rendered"

    if [[ ! -f "$installed" ]]; then
      new_units+=("$name")
    elif ! cmp -s "$rendered" "$installed"; then
      changed_units+=("$name")
    fi
  done

  if [[ ${#new_units[@]} -eq 0 && ${#changed_units[@]} -eq 0 ]]; then
    rm -rf "$tmp_dir"
    echo "新規=0 更新=0"
    return 0
  fi

  mkdir -p "$XVC_UNIT_DIR"

  # 置換されるものだけ退避する（新規は戻す対象が無い）
  if [[ -n "$backup_dir" ]]; then
    mkdir -p "$backup_dir"
    for name in "${changed_units[@]}"; do
      cp -p "${XVC_UNIT_DIR}/${name}" "${backup_dir}/${name}"
    done
    # 新規だったものはロールバック時に削除する必要があるため記録しておく
    : > "${backup_dir}/.added"
    if [[ ${#new_units[@]} -gt 0 ]]; then
      printf '%s\n' "${new_units[@]}" > "${backup_dir}/.added"
    fi
  fi

  for name in "${new_units[@]}" "${changed_units[@]}"; do
    install -m 644 "${tmp_dir}/${name}" "${XVC_UNIT_DIR}/${name}"
  done

  rm -rf "$tmp_dir"
  xvc_daemon_reload

  for name in "${new_units[@]}"; do
    [[ "$name" == *.timer ]] || continue
    xvc_enable_unit "$name"
  done

  echo "新規=${#new_units[@]} 更新=${#changed_units[@]}"
}

# sync_systemd_units が退避したユニットを書き戻す。
# 新規に追加されたユニットは削除する（更新前の状態に戻すため）。
restore_systemd_units() {
  local backup_dir="$1" name

  [[ -d "$backup_dir" ]] || return 0

  if [[ -f "${backup_dir}/.added" ]]; then
    while IFS= read -r name; do
      [[ -n "$name" ]] && rm -f "${XVC_UNIT_DIR}/${name}"
    done < "${backup_dir}/.added"
  fi

  for file in "${backup_dir}"/*; do
    [[ -f "$file" ]] || continue
    install -m 644 "$file" "${XVC_UNIT_DIR}/$(basename "$file")"
  done

  xvc_daemon_reload
}

# 起動に失敗したユニットを停止し、再起動ループ（Restart=always）を断ち切る。
# これを行わないとスクリプト終了後も 5 秒毎にクラッシュし続け、journal が汚れる。
stop_failed_service() {
  local unit="${1:-$XVC_SERVICE}"

  systemctl stop "$unit" 2>/dev/null || true
  err "再起動ループを避けるため ${unit} を停止しました（自動起動の設定は残っています）。"
  err "原因を解消したら次で起動してください:  sudo systemctl start ${unit}"
}
