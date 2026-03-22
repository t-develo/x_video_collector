import { describe, it, expect } from 'vitest';
import { TAG_COLORS, getTagColorCss } from '../../../src/frontend/js/utils/tagColor.js';

describe('tagColor', () => {
  describe('TAG_COLORS', () => {
    it('9件のエントリを持つ', () => {
      expect(TAG_COLORS).toHaveLength(9);
    });

    it('各エントリに value / name / label / css プロパティが存在する', () => {
      for (const color of TAG_COLORS) {
        expect(color).toHaveProperty('value');
        expect(color).toHaveProperty('name');
        expect(color).toHaveProperty('label');
        expect(color).toHaveProperty('css');
      }
    });

    it('value は 0〜8 の連番である', () => {
      const values = TAG_COLORS.map(c => c.value);
      expect(values).toEqual([0, 1, 2, 3, 4, 5, 6, 7, 8]);
    });

    it('各エントリの css は # で始まる16進カラーコードである', () => {
      for (const color of TAG_COLORS) {
        expect(color.css).toMatch(/^#[0-9a-f]{6}$/i);
      }
    });
  });

  describe('getTagColorCss', () => {
    it('value=0 (Red) → #ff4757 を返す', () => {
      expect(getTagColorCss(0)).toBe('#ff4757');
    });

    it('value=1 (Orange) → #ffa502 を返す', () => {
      expect(getTagColorCss(1)).toBe('#ffa502');
    });

    it('value=4 (Cyan) → #00d4aa を返す', () => {
      expect(getTagColorCss(4)).toBe('#00d4aa');
    });

    it('value=8 (Gray) → #888888 を返す', () => {
      expect(getTagColorCss(8)).toBe('#888888');
    });

    it('存在しない value (-1) → デフォルト #888888 を返す', () => {
      expect(getTagColorCss(-1)).toBe('#888888');
    });

    it('存在しない value (99) → デフォルト #888888 を返す', () => {
      expect(getTagColorCss(99)).toBe('#888888');
    });

    it('境界値: value=0 → 最初の色 (Red)', () => {
      expect(getTagColorCss(0)).toBe(TAG_COLORS[0].css);
    });

    it('境界値: value=8 → 最後の色 (Gray)', () => {
      expect(getTagColorCss(8)).toBe(TAG_COLORS[8].css);
    });
  });
});
