# X動画コレクター — プロジェクトルール

## プロジェクト概要

X（旧Twitter）の動画を個人的に収集・保存・管理するWebアプリケーション。
Azure サーバーレス構成で無料枠内運用を目指す。

## 技術スタック

| レイヤー | 技術 |
|---------|------|
| バックエンド | C# .NET 10, Azure Functions (Isolated Worker, Windows Consumption) |
| フロントエンド | Vanilla JS (ES2022+), HTML5, CSS3 — フレームワーク不使用 SPA |
| データベース | Azure SQL Database (Free Tier) |
| ストレージ | Azure Blob Storage |
| 認証 | Azure Static Web Apps 組み込み Entra ID 認証 |
| 動画DL | yt-dlp + ffmpeg（Process.Start で直接呼び出し） |
| テスト | xUnit + Moq (C#), Vitest + jsdom (JS) |
| CI/CD | GitHub Actions |
| IaC | Bicep |

## アーキテクチャ — クリーンアーキテクチャ

```
Domain（中心）→ Application → Infrastructure → Functions（最外層）
```

### 依存ルール（厳守）

| プロジェクト | 許可される依存先 |
|-------------|----------------|
| `XVideoCollector.Domain` | なし（依存ゼロ） |
| `XVideoCollector.Application` | Domain のみ |
| `XVideoCollector.Infrastructure` | Application, Domain |
| `XVideoCollector.Functions` | Application, Infrastructure（DI 登録のみ） |

- 依存は常に外→内の一方向のみ
- Domain に外部パッケージ参照を追加しない
- Infrastructure の具象クラスを Application や Domain から参照しない

### ソリューション構造

```
XVideoCollector.sln
├── src/
│   ├── api/
│   │   ├── XVideoCollector.Domain/
│   │   ├── XVideoCollector.Application/
│   │   ├── XVideoCollector.Infrastructure/
│   │   └── XVideoCollector.Functions/
│   └── frontend/
│       ├── index.html
│       ├── css/
│       ├── js/
│       └── staticwebapp.config.json
├── tests/
│   ├── XVideoCollector.Domain.Tests/
│   ├── XVideoCollector.Application.Tests/
│   ├── XVideoCollector.Infrastructure.Tests/
│   └── XVideoCollector.Functions.Tests/
├── infra/
│   ├── main.bicep
│   └── parameters.json
└── docs/
    └── sprints/
```

## ブランチ命名規約（厳守）

```
feature/sprint{N}/{実装内容}
```

### ルール

- 各スプリントの作業開始時に `main` から `feature/sprint{N}/{実装内容}` ブランチを切る
- スプリント完了後に `main` へ PR マージ
- 1つのスプリントで複数のサブブランチが必要な場合は `feature/sprint{N}/{サブ機能名}` で分割可
- ブランチ名の `{実装内容}` は英語小文字ケバブケース（例: `project-scaffolding`, `domain-entities`）

### Sprint ブランチ一覧

| Sprint | ブランチ |
|--------|---------|
| 0 | `feature/sprint0/skills-and-sprint-docs` |
| 1 | `feature/sprint1/project-scaffolding` |
| 2 | `feature/sprint2/domain-entities` |
| 3 | `feature/sprint3/application-usecases` |
| 4 | `feature/sprint4/repositories-blob` |
| 5 | `feature/sprint5/ytdlp-ffmpeg` |
| 6 | `feature/sprint6/api-endpoints` |
| 7 | `feature/sprint7/frontend-shell` |
| 8 | `feature/sprint8/register-page` |
| 9 | `feature/sprint9/video-list-page` |
| 10 | `feature/sprint10/tag-category-ui` |
| 11 | `feature/sprint11/search-filter` |
| 12 | `feature/sprint12/detail-player` |
| 13 | `feature/sprint13/responsive-polish` |
| 14 | `feature/sprint14/cicd-deploy` |

## コーディング規約

### C# (.NET 10 / C# 14)

- Nullable reference types 有効（`<Nullable>enable</Nullable>`）
- `file-scoped namespace` を使用
- `primary constructor` を積極活用
- `record` を DTO / 値オブジェクトに使用
- 非同期メソッドには `Async` サフィックス
- 名前空間: `XVideoCollector.{Layer}.{Feature}`

### Vanilla JS (ES2022+)

- ES Modules (`import`/`export`) を使用
- `const` 優先、`let` 必要時のみ、`var` 禁止
- クラスではなく関数 + モジュールパターン
- DOM 操作は `document.createElement` + `textContent`（innerHTML 禁止 — XSS 防止）
- すべての非同期処理は `async`/`await`

### CSS

- CSS カスタムプロパティ（変数）でテーマ管理
- Industrial Minimal ダークテーマ
- モバイルファーストではなくデスクトップファーストで記述

## コミットメッセージ規約

```
{type}: {簡潔な説明}
```

type: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `style`, `ci`

例:
- `feat: add Video entity and TweetUrl value object`
- `fix: handle yt-dlp timeout on large files`
- `test: add unit tests for RegisterVideoUseCase`

## カスタム Skills

本プロジェクトでは以下のカスタム Skills を `.claude/skills/` 配下に定義している:

| Skill | 用途 |
|-------|------|
| `clean-architecture-dotnet` | C# クリーンアーキテクチャのコード生成標準化 |
| `vanilla-js-spa` | フレームワーク不使用 SPA の開発パターン標準化 |
| `azure-serverless-deploy` | Azure サーバーレスのデプロイ自動化 |
| `yt-dlp-integration` | yt-dlp の C# プロセス呼び出し安全実装 |
