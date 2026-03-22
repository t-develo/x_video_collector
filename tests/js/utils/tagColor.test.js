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
    it('TAG_COLORS の全エントリに対して正しい css を返す', () => {
      for (const color of TAG_COLORS) {
        expect(getTagColorCss(color.value)).toBe(color.css);
      }
    });

    it('存在しない value はデフォルト #888888 を返す', () => {
      expect(getTagColorCss(-1)).toBe('#888888');
      expect(getTagColorCss(99)).toBe('#888888');
    });
  });
});
