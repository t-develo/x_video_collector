import { describe, it, expect } from 'vitest';
import { formatDate, formatFileSize, formatDuration } from '../../../src/frontend/js/utils/format.js';

describe('format utils', () => {
  describe('formatFileSize', () => {
    it('0 バイトを "0 B" で表示する', () => {
      expect(formatFileSize(0)).toBe('0 B');
    });

    it('バイト単位で表示する', () => {
      expect(formatFileSize(512)).toBe('512 B');
    });

    it('KB 単位で表示する', () => {
      expect(formatFileSize(1024)).toBe('1.0 KB');
    });

    it('MB 単位で表示する', () => {
      expect(formatFileSize(1024 * 1024)).toBe('1.0 MB');
    });

    it('GB 単位で表示する', () => {
      expect(formatFileSize(1024 * 1024 * 1024)).toBe('1.0 GB');
    });

    it('負の値は "---" を返す', () => {
      expect(formatFileSize(-1)).toBe('---');
    });
  });

  describe('formatDuration', () => {
    it('秒を mm:ss 形式で表示する', () => {
      expect(formatDuration(65)).toBe('01:05');
    });

    it('1時間以上は h:mm:ss 形式で表示する', () => {
      expect(formatDuration(3661)).toBe('1:01:01');
    });

    it('0 秒は "不明" を返す（未取得の場合）', () => {
      expect(formatDuration(0)).toBe('不明');
    });

    it('負の値は "--:--" を返す', () => {
      expect(formatDuration(-1)).toBe('--:--');
    });

    it('NaN は "--:--" を返す', () => {
      expect(formatDuration(NaN)).toBe('--:--');
    });
  });

  describe('formatDate', () => {
    it('有効な日付文字列をフォーマットする', () => {
      const result = formatDate('2024-01-15T10:30:00Z');
      expect(result).toMatch(/2024/);
      expect(result).toMatch(/01/);
    });

    it('無効な日付は "---" を返す', () => {
      expect(formatDate('not-a-date')).toBe('---');
    });
  });
});
