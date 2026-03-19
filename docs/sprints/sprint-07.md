# Sprint 7: Frontend Shell

## 目的

SPA のシェル（共通レイアウト、ルーター、API クライアント、状態管理）の実装。

## ブランチ

`feature/sprint7/frontend-shell`

## タスク

### 7.1 index.html

- SPA エントリポイント
- CSS/JS の読み込み（ES Modules）
- アプリコンテナ要素

### 7.2 CSS 基盤

- `variables.css` — Industrial Minimal ダークテーマ変数
- `reset.css` — ブラウザリセット
- `layout.css` — ヘッダー、サイドバー、メインコンテンツ

### 7.3 JavaScript 基盤

- `app.js` — アプリ初期化
- `router.js` — Hash ベースルーター
- `api.js` — fetch ラッパー（API クライアント）
- `state.js` — Pub/Sub 状態管理
- `utils/dom.js` — DOM ヘルパー
- `utils/format.js` — 日付・ファイルサイズフォーマッター

### 7.4 共通コンポーネント

- ヘッダーコンポーネント（ロゴ、ナビゲーション）
- ローディングスピナー
- トースト通知

### 7.5 テスト

- ルーターのユニットテスト
- API クライアントのユニットテスト（fetch Mock）
- 状態管理のユニットテスト
- DOM ヘルパーのユニットテスト

## 完了条件

- [ ] SPA シェルがブラウザで表示される
- [ ] ルーターが正しく動作する
- [ ] ダークテーマが適用されている
- [ ] テストがすべてパスする
