# Sprint 16: セキュリティ強化

## 目的

認証・認可の防御層を追加し、攻撃面を最小化する。

## ブランチ

`feature/sprint16/security-hardening`

## 背景・問題点

| # | 問題 | 深刻度 |
|---|------|--------|
| 1 | Functions アプリへの直接アクセスに対する認証防御なし | 🔴 HIGH |
| 2 | `pageSize` の上限チェックなし（100000 等でページングが無効化される） | 🟠 MEDIUM |
| 3 | Tag / Category の Name にドメイン層バリデーション不足 | 🟠 MEDIUM |
| 4 | CategoryName に一意制約なし | 🟠 MEDIUM |
| 5 | VideoTag に外部キー制約なし | 🟡 MEDIUM |
| 6 | `ExceptionMiddleware` の文字列ベース例外分類 | 🟡 MEDIUM |

## タスク

### 16.1 Functions 直接アクセス防御

Azure Static Web Apps の認証を迂回した Functions への直接アクセスをブロックする。

#### 実装内容

- `AuthMiddleware.cs` を追加:
  1. HTTP Trigger 以外（Queue Trigger 等）はスキップ
  2. 環境変数 `SKIP_AUTH=true` の場合はスキップ（ローカル開発用）
  3. `X-MS-CLIENT-PRINCIPAL` ヘッダーがない場合は 401 Unauthorized を返す
- `Program.cs` に `AuthMiddleware` を登録（`ExceptionMiddleware` の後）

**修正対象:** 全 HTTP Trigger 関数（VideoFunctions, TagFunctions, CategoryFunctions）

### 16.2 入力バリデーション強化

#### 実装内容

1. **pageSize の上限設定（最大 100）**
   - `ListVideosUseCase.cs`: `pageSize = Math.Min(pageSize, 100)` を追加
   - `SearchVideosUseCase.cs`: 同上

2. **Tag / Category の Name に最大長チェック（100文字）**
   - `Tag.Create()` / `Tag.Update()`: `name.Length > 100` の場合に `ArgumentException` を投げる
   - `Category.Create()` / `Category.Update()`: 同上

3. **Category の Name に一意制約（DB レベル）**
   - `CategoryConfiguration.cs`: `HasIndex(c => c.Name).IsUnique()` を追加

4. **VideoTag に外部キー制約**
   - `VideoTagConfiguration.cs`: Video と Tag に対する外部キーを定義

### 16.3 例外処理の改善

`ExceptionMiddleware` の文字列マッチングを型ベースのパターンマッチに変更する。

#### 実装内容

1. `NotFoundException` 基底クラスを Application/Exceptions に追加
2. `VideoNotFoundException` を `Exception` から `NotFoundException` に変更
3. `ExceptionMiddleware.cs` の `InvalidOperationException` 文字列マッチを `NotFoundException` 型マッチに変更

## 完了条件

- [ ] `X-MS-CLIENT-PRINCIPAL` ヘッダーなしのリクエストに 401 を返す
- [ ] `SKIP_AUTH=true` 時は認証をスキップする
- [ ] `pageSize=200` のリクエストが `pageSize=100` に制限される
- [ ] Tag/Category の Name に 101 文字を指定すると `ArgumentException` が投げられる
- [ ] Category の Name にユニーク制約が DB レベルで適用される
- [ ] VideoTag に外部キー制約が設定される
- [ ] `ExceptionMiddleware` が文字列マッチングではなく型ベースで例外を分類する
- [ ] `dotnet test` で全 C# テストが通過する
- [ ] `npm test` で全 JS テストが通過する
- [ ] CLAUDE.md の規約違反がない
