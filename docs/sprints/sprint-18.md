# Sprint 18: 動画メモ & ダウンロード失敗理由記録

## 目的

動画に個人メモを追加できる機能と、ダウンロード失敗時の失敗理由を記録・表示する機能を追加する。
これにより、ユーザーが動画に文脈情報を付与し、失敗時のトラブルシューティングを容易にする。

## ブランチ

`feature/sprint18/notes-failure-reason`

## 背景・問題点

| # | 問題 | 深刻度 |
|---|------|--------|
| 1 | 動画に個人メモ・補足情報を付与できない | 🟡 LOW |
| 2 | ダウンロード失敗時の理由が記録されず、原因調査が困難 | 🟠 MEDIUM |

## タスク

### 18.1 動画メモ機能

ユーザーが動画に自由テキストのメモを付けられるようにする。

#### 実装内容

**Domain 層:**
- `Video.cs` — `Notes` プロパティ（`string?`, 最大 2000 文字）を追加
- `Video.UpdateNotes(string? notes, TimeProvider timeProvider)` メソッドを追加
  - `notes` が 2000 文字を超える場合は `ArgumentException` を投げる

**Infrastructure 層:**
- `VideoConfiguration.cs` — `Notes` カラムの設定（`HasMaxLength(2000)`, nullable）を追加

**Application 層:**
- `VideoDto.cs` — `Notes` フィールドを追加
- `UpdateVideoRequest.cs` — `Notes` フィールドを追加（`string?`）
- `UpdateVideoUseCase.cs` — `video.UpdateNotes(request.Notes, timeProvider)` を呼び出す
- `VideoMapper.cs` — `Notes` をマッピングに追加

**Frontend:**
- `videoDetail.js` — メモ入力用 `<textarea>` を詳細ページに追加し、保存処理に含める

### 18.2 ダウンロード失敗理由記録

ダウンロード失敗時に例外メッセージを `FailureReason` として記録し、詳細ページに表示する。

#### 実装内容

**Domain 層:**
- `Video.cs` — `FailureReason` プロパティ（`string?`, 最大 2000 文字）を追加
- `Video.MarkFailed(string? failureReason, TimeProvider timeProvider)` シグネチャ変更
  - `MarkReady()` 呼び出し時に `FailureReason` を `null` にリセットする（再ダウンロード成功時にクリア）

**Infrastructure 層:**
- `VideoConfiguration.cs` — `FailureReason` カラムの設定（`HasMaxLength(2000)`, nullable）を追加

**Application 層:**
- `VideoDto.cs` — `FailureReason` フィールドを追加
- `VideoListItemDto.cs` — `FailureReason` フィールドを追加（一覧で Failed 状態のヒント表示用）
- `DownloadVideoUseCase.cs` — `catch` ブロックで `ex.Message` を `MarkFailed()` に渡す
- `VideoMapper.cs` — `FailureReason` をマッピングに追加

**Frontend:**
- `videoDetail.js` — Failed 状態の場合に `failureReason` を表示するセクションを追加
- `videoCard.js` — Failed 状態のカードに `failureReason` のツールチップ表示を追加

## 完了条件

- [ ] `Video.Notes` に 2001 文字を指定すると `ArgumentException` が投げられる
- [ ] `Video.UpdateNotes()` を呼ぶと `UpdatedAt` が更新される
- [ ] `Video.MarkFailed("reason", ...)` で `FailureReason` が記録される
- [ ] `Video.MarkReady(...)` を呼ぶと `FailureReason` が `null` にリセットされる
- [ ] `PUT /api/videos/{id}` に `notes` を含めると動画のメモが更新される
- [ ] `GET /api/videos/{id}` のレスポンスに `notes` と `failureReason` が含まれる
- [ ] ダウンロード失敗時に `failureReason` に例外メッセージが記録される
- [ ] フロントエンドの詳細ページにメモ編集欄が表示される
- [ ] フロントエンドの詳細ページに失敗理由が表示される（Failed 状態の場合）
- [ ] `dotnet test` で全 C# テストが通過する
- [ ] `npm test` で全 JS テストが通過する
- [ ] CLAUDE.md の規約違反がない
