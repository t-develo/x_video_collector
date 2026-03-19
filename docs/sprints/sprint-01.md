# Sprint 1: Project Scaffolding

## 目的

ソリューション構造の作成と、すべてのプロジェクトの初期セットアップ。

## ブランチ

`feature/sprint1/project-scaffolding`

## タスク

### 1.1 ソリューション作成

- `XVideoCollector.sln` をルートに作成
- ディレクトリ構造: `src/api/`, `src/frontend/`, `tests/`, `infra/`, `docs/`

### 1.2 C# プロジェクト作成

| プロジェクト | テンプレート | パス |
|-------------|------------|------|
| `XVideoCollector.Domain` | classlib | `src/api/XVideoCollector.Domain/` |
| `XVideoCollector.Application` | classlib | `src/api/XVideoCollector.Application/` |
| `XVideoCollector.Infrastructure` | classlib | `src/api/XVideoCollector.Infrastructure/` |
| `XVideoCollector.Functions` | Azure Functions Isolated | `src/api/XVideoCollector.Functions/` |

### 1.3 テストプロジェクト作成

| プロジェクト | パス |
|-------------|------|
| `XVideoCollector.Domain.Tests` | `tests/XVideoCollector.Domain.Tests/` |
| `XVideoCollector.Application.Tests` | `tests/XVideoCollector.Application.Tests/` |
| `XVideoCollector.Infrastructure.Tests` | `tests/XVideoCollector.Infrastructure.Tests/` |
| `XVideoCollector.Functions.Tests` | `tests/XVideoCollector.Functions.Tests/` |

### 1.4 プロジェクト参照設定

- Application → Domain
- Infrastructure → Application, Domain
- Functions → Application, Infrastructure
- 各テストプロジェクト → 対応する本体プロジェクト

### 1.5 共通設定

- `Directory.Build.props` で共通プロパティ（TargetFramework, Nullable, ImplicitUsings）
- `.gitignore` に .NET / Node.js / Azure 用エントリ追加
- `.editorconfig` でコードスタイル統一

### 1.6 フロントエンド初期構造

- `src/frontend/index.html` スケルトン作成
- `src/frontend/css/` ディレクトリ作成
- `src/frontend/js/` ディレクトリ作成
- `src/frontend/staticwebapp.config.json` 作成

### 1.7 ビルド確認

- `dotnet build` が成功すること
- `dotnet test` が成功すること（テストは空でも可）

## 完了条件

- [ ] すべてのプロジェクトが作成され、ソリューションに追加されている
- [ ] プロジェクト参照が正しく設定されている
- [ ] `dotnet build` が成功する
- [ ] `dotnet test` が成功する
- [ ] フロントエンドの初期ディレクトリ構造が存在する
