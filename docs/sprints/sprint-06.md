# Sprint 6: API Endpoints

## 目的

Azure Functions HTTP トリガーによる REST API エンドポイントの実装。

## ブランチ

`feature/sprint6/api-endpoints`

## タスク

### 6.1 Program.cs セットアップ

- Isolated Worker モデルの構成
- DI 登録（Infrastructure, Application）
- JSON シリアライゼーション設定
- ミドルウェア登録

### 6.2 API エンドポイント

| メソッド | ルート | 説明 |
|---------|--------|------|
| POST | `/api/videos` | 動画登録 |
| GET | `/api/videos` | 動画一覧（ページネーション） |
| GET | `/api/videos/{id}` | 動画詳細 |
| PUT | `/api/videos/{id}` | 動画更新 |
| DELETE | `/api/videos/{id}` | 動画削除 |
| GET | `/api/videos/{id}/stream` | 動画ストリーム URL 取得 |
| GET | `/api/videos/search?q=` | 動画検索 |
| GET | `/api/tags` | タグ一覧 |
| POST | `/api/tags` | タグ作成 |
| PUT | `/api/tags/{id}` | タグ更新 |
| DELETE | `/api/tags/{id}` | タグ削除 |
| GET | `/api/categories` | カテゴリ一覧 |
| POST | `/api/categories` | カテゴリ作成 |
| PUT | `/api/categories/{id}` | カテゴリ更新 |
| DELETE | `/api/categories/{id}` | カテゴリ削除 |

### 6.3 エラーハンドリングミドルウェア

- 統一エラーレスポンス形式
- バリデーションエラー → 400
- 未検出 → 404
- サーバーエラー → 500（詳細はログのみ）

### 6.4 host.json 設定

- ルーティングプレフィックス設定
- ログレベル設定

### 6.5 テスト

- 各エンドポイントのユニットテスト（ユースケース Mock）
- エラーレスポンスのテスト

## 完了条件

- [ ] すべてのエンドポイントが実装されている
- [ ] エラーハンドリングが統一されている
- [ ] `func start` でローカル起動できる
- [ ] テストがすべてパスする
