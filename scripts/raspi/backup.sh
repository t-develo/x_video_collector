#!/usr/bin/env bash
# X Video Collector — SQLite データベースのバックアップ
# xvideocollector-backup.service (systemd timer) から日次で実行される。
#
# 動画ファイル本体は再取得できるが、タグ・カテゴリ・メモ等のメタデータは
# 失うと復元できないため DB のみをバックアップする。

set -euo pipefail

RETENTION_COUNT="${XVC_BACKUP_RETENTION:-14}"

# ConnectionStrings__SqlDb = "Data Source=/var/lib/xvideocollector/xvideocollector.db"
DB_PATH="${ConnectionStrings__SqlDb#Data Source=}"
DB_PATH="${DB_PATH#"${DB_PATH%%[![:space:]]*}"}"

if [[ -z "$DB_PATH" || ! -f "$DB_PATH" ]]; then
  echo "ERROR: データベースが見つかりません: '${DB_PATH}'" >&2
  exit 1
fi

BACKUP_DIR="$(dirname "$DB_PATH")/backups"
mkdir -p "$BACKUP_DIR"

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
DEST="${BACKUP_DIR}/xvideocollector-${TIMESTAMP}.db"

# サービス稼働中でも一貫したスナップショットを取るため .backup を使う
# （単純な cp は書き込み途中のページを掴む可能性がある）
if command -v sqlite3 &>/dev/null; then
  sqlite3 "$DB_PATH" ".backup '${DEST}'"
else
  echo "WARN: sqlite3 が無いためファイルコピーでバックアップします" >&2
  cp "$DB_PATH" "$DEST"
fi

echo "バックアップ作成: ${DEST}"

# 世代ローテート（新しい順に RETENTION_COUNT 件を残す）
mapfile -t OLD_BACKUPS < <(
  find "$BACKUP_DIR" -maxdepth 1 -name 'xvideocollector-*.db' -printf '%T@ %p\n' \
    | sort -rn \
    | tail -n "+$((RETENTION_COUNT + 1))" \
    | cut -d' ' -f2-
)

for old in "${OLD_BACKUPS[@]}"; do
  [[ -n "$old" ]] || continue
  rm -f "$old"
  echo "古いバックアップを削除: ${old}"
done
