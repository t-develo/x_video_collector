import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api, ApiError } from '../../src/frontend/js/api.js';

describe('api', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  describe('get', () => {
    it('成功時に JSON を返す', async () => {
      const data = { id: 1, title: 'test' };
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => data,
      });

      const result = await api.get('/videos');
      expect(result).toEqual(data);
      expect(fetch).toHaveBeenCalledWith('/api/videos', expect.objectContaining({
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }));
    });

    it('204 の場合は null を返す', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
        json: async () => null,
      });

      const result = await api.delete('/videos/1');
      expect(result).toBeNull();
    });

    it('エラー時に ApiError をスローする', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        text: async () => 'Video not found',
      });

      await expect(api.get('/videos/999')).rejects.toThrow(ApiError);
    });
  });

  describe('post', () => {
    it('リクエストボディを JSON でシリアライズして送信する', async () => {
      const body = { tweetUrl: 'https://x.com/user/status/123' };
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 201,
        json: async () => ({ id: 'new-id', ...body }),
      });

      await api.post('/videos', body);
      expect(fetch).toHaveBeenCalledWith('/api/videos', expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(body),
      }));
    });
  });

  describe('ApiError', () => {
    it('status と body を持つ', () => {
      const err = new ApiError(422, 'Unprocessable Entity', 'Invalid URL');
      expect(err.status).toBe(422);
      expect(err.body).toBe('Invalid URL');
      expect(err.name).toBe('ApiError');
    });
  });
});
