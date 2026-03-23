// utils/format.js — 日付・ファイルサイズ等フォーマッター

const DATE_FORMATTER = new Intl.DateTimeFormat('ja-JP', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
});

/**
 * ISO 日付文字列を日本語形式でフォーマットする
 * @param {string} isoString
 * @returns {string}
 */
export function formatDate(isoString) {
  const date = new Date(isoString);
  if (isNaN(date.getTime())) return '---';
  return DATE_FORMATTER.format(date);
}

/**
 * バイト数を人間が読みやすい形式に変換する
 * @param {number} bytes
 * @returns {string}
 */
export function formatFileSize(bytes) {
  if (bytes === 0) return '0 B';
  if (!Number.isFinite(bytes) || bytes < 0) return '---';

  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exp = Math.min(Math.floor(Math.log2(bytes) / 10), units.length - 1);
  const value = bytes / Math.pow(1024, exp);

  return `${value.toFixed(exp === 0 ? 0 : 1)} ${units[exp]}`;
}

/**
 * 秒数を mm:ss 形式に変換する。
 * 0 の場合は再生時間が未取得（ffprobe 失敗など）を意味し '不明' を返す。
 * @param {number} seconds
 * @returns {string}
 */
export function formatDuration(seconds) {
  if (seconds === 0) return '不明';
  if (!Number.isFinite(seconds) || seconds < 0) return '--:--';

  const totalSec = Math.floor(seconds);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;

  if (h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}
