# Sprint 14: CI/CD & Deploy

## 目的

GitHub Actions による CI/CD パイプラインの構築と Azure へのデプロイ。

## ブランチ

`feature/sprint14/cicd-deploy`

## タスク

### 14.1 CI ワークフロー（`.github/workflows/ci.yml`）

- PR 時に自動実行
- .NET ビルド + テスト
- JS テスト（Vitest）
- ビルド成果物のアーティファクト保存

### 14.2 デプロイワークフロー（`.github/workflows/deploy.yml`）

- main マージ時に自動実行
- Azure Login（OIDC / Workload Identity Federation）
- Bicep によるインフラデプロイ
- Functions デプロイ
- Static Web Apps デプロイ

### 14.3 Bicep テンプレート完成

- `infra/main.bicep` — モジュール呼び出し
- `infra/modules/` — 各リソースモジュール
- `infra/parameters.json` — パラメータ定義

### 14.4 Azure リソース設定

- Entra ID アプリ登録（認証用）
- GitHub OIDC 設定（デプロイ用）
- シークレット設定（GitHub Secrets）

### 14.5 デプロイ検証

- ステージング環境への初回デプロイ
- エンドツーエンド動作確認
- yt-dlp / ffmpeg バイナリの動作確認

### 14.6 ドキュメント

- デプロイ手順書
- 環境変数一覧
- トラブルシューティングガイド

## 完了条件

- [ ] CI が PR で自動実行される
- [ ] main マージで自動デプロイされる
- [ ] Azure 上で全機能が動作する
- [ ] 認証が正しく機能する
- [ ] デプロイ手順が文書化されている
