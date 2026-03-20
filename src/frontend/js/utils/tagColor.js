// utils/tagColor.js — タグ色ユーティリティ

/** TagColor enum 値と表示用マッピング */
export const TAG_COLORS = [
  { value: 0, name: 'Red', label: '赤', css: '#ff4757' },
  { value: 1, name: 'Orange', label: 'オレンジ', css: '#ffa502' },
  { value: 2, name: 'Yellow', label: '黄', css: '#ffd43b' },
  { value: 3, name: 'Green', label: '緑', css: '#2ed573' },
  { value: 4, name: 'Cyan', label: 'シアン', css: '#00d4aa' },
  { value: 5, name: 'Blue', label: '青', css: '#3b82f6' },
  { value: 6, name: 'Purple', label: '紫', css: '#a855f7' },
  { value: 7, name: 'Pink', label: 'ピンク', css: '#ec4899' },
  { value: 8, name: 'Gray', label: 'グレー', css: '#888888' },
];

/**
 * 色の CSS 値を取得する
 * @param {number} colorValue
 * @returns {string}
 */
export function getTagColorCss(colorValue) {
  return TAG_COLORS.find(c => c.value === colorValue)?.css ?? '#888888';
}
