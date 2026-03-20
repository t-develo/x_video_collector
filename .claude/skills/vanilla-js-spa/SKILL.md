# Skill: vanilla-js-spa

フレームワーク不使用の Vanilla JS SPA 開発パターンを標準化するスキル。

## 技術要件

- ES2022+ (ES Modules)
- HTML5, CSS3 (カスタムプロパティによるテーマ管理)
- フレームワーク・ライブラリ不使用
- ビルドツール不使用（ブラウザネイティブ ESM）

## ディレクトリ構成

```
src/frontend/
├── index.html              # SPA エントリポイント（単一 HTML）
├── css/
│   ├── variables.css        # CSS カスタムプロパティ（テーマ変数）
│   ├── reset.css            # リセット CSS
│   ├── layout.css           # レイアウト共通
│   └── components/          # コンポーネント別 CSS
│       ├── header.css
│       ├── video-card.css
│       └── modal.css
├── js/
│   ├── app.js               # アプリ初期化・ルーター起動
│   ├── router.js            # クライアントサイドルーター
│   ├── api.js               # API クライアント（fetch ラッパー）
│   ├── state.js             # シンプルな状態管理
│   ├── pages/               # ページモジュール
│   │   ├── register.js
│   │   ├── video-list.js
│   │   ├── video-detail.js
│   │   └── tag-category.js
│   ├── components/          # 再利用可能 UI コンポーネント
│   │   ├── video-card.js
│   │   ├── search-bar.js
│   │   ├── tag-chip.js
│   │   └── modal.js
│   └── utils/               # ユーティリティ
│       ├── dom.js
│       └── format.js
└── staticwebapp.config.json # Azure Static Web Apps 設定
```

## コーディングルール

### モジュールパターン

```javascript
// ページモジュール — 関数エクスポートパターン
// pages/register.js

import { postVideo } from '../api.js';
import { createElement, clearChildren } from '../utils/dom.js';

export function renderRegisterPage(container) {
  clearChildren(container);  // innerHTML = '' は禁止。clearChildren() を使う

  const form = createRegisterForm();
  container.appendChild(form);
}

function createRegisterForm() {
  const form = document.createElement('form');
  // ... DOM 構築
  return form;
}
```

### DOM 操作ルール（厳守）

- `innerHTML` の使用は一切禁止（XSS 防止）。`innerHTML = ''` による要素クリアも禁止
  - 要素のクリアには `clearChildren(container)` を使用する（`utils/dom.js` 提供）
- `document.createElement` + `textContent` で DOM を構築
- ユーザー入力値は必ず `textContent` または `setAttribute` 経由で設定
- インラインスタイル（`el.style.cssText`, `el.style.xxx`）を使わず、CSS ファイルにクラスとして定義する

```javascript
// ✅ 正しい
import { clearChildren } from '../utils/dom.js';
clearChildren(container);
const el = document.createElement('span');
el.textContent = userInput;

// ❌ 禁止
container.innerHTML = '';
el.innerHTML = userInput;
el.style.cssText = 'color: red; font-size: 2rem;';
```

### DOM ヘルパー（utils/dom.js）

```javascript
/**
 * 要素を生成するヘルパー
 * @param {string} tag - タグ名
 * @param {Object} attrs - 属性オブジェクト
 * @param {string|Node|Array<Node>} children - 子要素
 * @returns {HTMLElement}
 */
export function createElement(tag, attrs = {}, children = null) {
  const el = document.createElement(tag);

  for (const [key, value] of Object.entries(attrs)) {
    if (key === 'className') {
      el.className = value;
    } else if (key === 'textContent') {
      el.textContent = value;
    } else if (key.startsWith('on')) {
      el.addEventListener(key.slice(2).toLowerCase(), value);
    } else {
      el.setAttribute(key, value);
    }
  }

  if (children !== null) {
    if (typeof children === 'string') {
      el.textContent = children;
    } else if (Array.isArray(children)) {
      children.forEach(child => el.appendChild(child));
    } else {
      el.appendChild(children);
    }
  }

  return el;
}
```

### ルーター

```javascript
// router.js — Hash ベースルーティング

const routes = new Map();

export function addRoute(path, handler) {
  routes.set(path, handler);
}

export function navigateTo(path) {
  window.location.hash = `#${path}`;
}

export function startRouter(container) {
  const handleRoute = () => {
    const path = window.location.hash.slice(1) || '/';
    const handler = routes.get(path);
    if (handler) {
      handler(container);
    } else {
      render404(container);
    }
  };

  window.addEventListener('hashchange', handleRoute);
  handleRoute();
}
```

### 非同期処理ルール（厳守）

- すべての非同期処理は `async`/`await` を使用する。`.then()` チェーンは禁止
- データ取得はサーバーサイドページングを活用し、全件取得（`pageSize=1000` 等）を避ける

```javascript
// ✅ 正しい
async function loadUserInfo() {
  try {
    const response = await fetch('/.auth/me');
    const data = await response.json();
    // ...
  } catch {
    // エラーハンドリング
  }
}

// ❌ 禁止
function loadUserInfo() {
  fetch('/.auth/me')
    .then(r => r.json())
    .then(data => { /* ... */ })
    .catch(() => {});
}
```

### API クライアント

```javascript
// api.js — fetch ラッパー

const BASE_URL = '/api';

async function request(path, options = {}) {
  const url = `${BASE_URL}${path}`;
  const response = await fetch(url, {
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  });

  if (!response.ok) {
    throw new Error(`API Error: ${response.status} ${response.statusText}`);
  }

  return response.status === 204 ? null : response.json();
}

export const api = {
  get: (path) => request(path),
  post: (path, body) => request(path, { method: 'POST', body: JSON.stringify(body) }),
  put: (path, body) => request(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: (path) => request(path, { method: 'DELETE' }),
};
```

### 状態管理

```javascript
// state.js — シンプルな Pub/Sub 状態管理

const state = {};
const listeners = new Map();

export function getState(key) {
  return state[key];
}

export function setState(key, value) {
  state[key] = value;
  const keyListeners = listeners.get(key) || [];
  keyListeners.forEach(fn => fn(value));
}

export function subscribe(key, fn) {
  if (!listeners.has(key)) {
    listeners.set(key, []);
  }
  listeners.get(key).push(fn);
  return () => {
    const arr = listeners.get(key);
    const idx = arr.indexOf(fn);
    if (idx >= 0) arr.splice(idx, 1);
  };
}
```

## CSS テーマ（Industrial Minimal ダークテーマ）

```css
/* variables.css */
:root {
  /* カラーパレット */
  --color-bg-primary: #0a0a0a;
  --color-bg-secondary: #141414;
  --color-bg-tertiary: #1e1e1e;
  --color-bg-hover: #282828;

  --color-text-primary: #e0e0e0;
  --color-text-secondary: #888888;
  --color-text-muted: #555555;

  --color-accent: #00d4aa;
  --color-accent-hover: #00f0c0;
  --color-danger: #ff4757;
  --color-warning: #ffa502;

  --color-border: #2a2a2a;
  --color-border-focus: #00d4aa;

  /* タイポグラフィ */
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
  --font-mono: 'JetBrains Mono', 'Fira Code', monospace;

  --text-xs: 0.75rem;
  --text-sm: 0.875rem;
  --text-base: 1rem;
  --text-lg: 1.125rem;
  --text-xl: 1.25rem;

  /* スペーシング */
  --space-xs: 0.25rem;
  --space-sm: 0.5rem;
  --space-md: 1rem;
  --space-lg: 1.5rem;
  --space-xl: 2rem;

  /* ボーダー */
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;

  /* シャドウ */
  --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.5);
  --shadow-md: 0 4px 8px rgba(0, 0, 0, 0.5);

  /* トランジション */
  --transition-fast: 150ms ease;
  --transition-normal: 250ms ease;
}
```

## テスト（Vitest + jsdom）

```javascript
// tests/js/pages/register.test.js
import { describe, it, expect, vi } from 'vitest';
import { renderRegisterPage } from '../../../src/frontend/js/pages/register.js';

describe('renderRegisterPage', () => {
  it('should render a form element', () => {
    const container = document.createElement('div');
    renderRegisterPage(container);

    const form = container.querySelector('form');
    expect(form).not.toBeNull();
  });
});
```

### Vitest 設定

```javascript
// vitest.config.js
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    include: ['tests/js/**/*.test.js'],
  },
});
```

## staticwebapp.config.json

```json
{
  "auth": {
    "identityProviders": {
      "azureActiveDirectory": {
        "registration": {
          "openIdIssuer": "https://login.microsoftonline.com/{TENANT_ID}/v2.0",
          "clientIdSettingName": "AAD_CLIENT_ID",
          "clientSecretSettingName": "AAD_CLIENT_SECRET"
        }
      }
    }
  },
  "routes": [
    { "route": "/api/*", "allowedRoles": ["authenticated"] },
    { "route": "/*", "allowedRoles": ["authenticated"] }
  ],
  "responseOverrides": {
    "401": {
      "redirect": "/.auth/login/aad",
      "statusCode": 302
    }
  },
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/css/*", "/js/*", "/api/*"]
  }
}
```
