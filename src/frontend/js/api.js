// api.js — fetch ラッパー API クライアント

const BASE_URL = '/api';

/**
 * HTTP リクエストを送信する
 * @param {string} path - API パス（BASE_URL からの相対パス）
 * @param {RequestInit} options
 * @returns {Promise<any>}
 */
async function request(path, options = {}) {
  const url = `${BASE_URL}${path}`;
  const response = await fetch(url, {
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    ...options,
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => response.statusText);
    throw new ApiError(response.status, response.statusText, errorText);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

/**
 * API エラークラス
 */
export class ApiError extends Error {
  /**
   * @param {number} status
   * @param {string} statusText
   * @param {string} body
   */
  constructor(status, statusText, body) {
    super(`API Error: ${status} ${statusText}`);
    this.name = 'ApiError';
    this.status = status;
    this.statusText = statusText;
    this.body = body;
  }
}

export const api = {
  /**
   * GET リクエスト
   * @param {string} path
   * @returns {Promise<any>}
   */
  get(path) {
    return request(path);
  },

  /**
   * 統計情報を取得する
   * @returns {Promise<{totalCount:number, pendingCount:number, downloadingCount:number, processingCount:number, readyCount:number, failedCount:number, totalFileSizeBytes:number}>}
   */
  getStats() {
    return request('/stats');
  },

  /**
   * POST リクエスト
   * @param {string} path
   * @param {unknown} body
   * @returns {Promise<any>}
   */
  post(path, body) {
    return request(path, { method: 'POST', body: JSON.stringify(body) });
  },

  /**
   * PUT リクエスト
   * @param {string} path
   * @param {unknown} body
   * @returns {Promise<any>}
   */
  put(path, body) {
    return request(path, { method: 'PUT', body: JSON.stringify(body) });
  },

  /**
   * DELETE リクエスト
   * @param {string} path
   * @returns {Promise<any>}
   */
  delete(path) {
    return request(path, { method: 'DELETE' });
  },
};
