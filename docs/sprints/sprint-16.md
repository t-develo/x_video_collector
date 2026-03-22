# Sprint 16: ダウンロードライフサイクル改善

## 目的

動画登録からダウンロード完了までのライフサイクルを改善し、ユーザー体験と堅牢性を向上させる。

## ブランチ

`feature/sprint16/download-lifecycle`

## 背景・問題点

| # | 問題 | 深刻度 |
|---|------|--------|
| 1 | TweetUrl の重複チェックがなく、同一動画を複数回登録できてしまう | 🔴 High — データ整合性の問題 |
| 2 | Failed 状態の動画を再試行する手段がない（削除して再登録するしかない） | 🟠 High — UX の問題 |
| 3 | ダウンロード中の動画（Pending/Downloading/Processing）の状態変化をフロントエンドでリアルタイムに確認できない | 🟡 Medium — UX の問題 |

## タスク

### 16.1 TweetUrl 重複防止

同一 TweetId の動画が複数登録されないようにする。

#### 実装内容

- `IVideoRepository` に `FindByTweetIdAsync(string tweetId)` を追加
- `VideoRepository` に実装（TweetId カラムで検索）
- `VideoConfiguration` に TweetUrl のユニーク制約（DB レベル）を追加
- Application 層に `DuplicateTweetUrlException` を追加
- `RegisterVideoUseCase.ExecuteAsync()` で重複チェック → 重複の場合に `DuplicateTweetUrlException` を投げる
- `VideoFunctions.RegisterVideoAsync()` で `DuplicateTweetUrlException` をキャッチ → 409 Conflict を返す

#### 動作

- 同一の TweetUrl（または正規化後に同一の TweetId）を持つ動画が既に存在する場合、409 Conflict を返す
- フロントエンドは既存の 409 ハンドリングにより、適切なエラーメッセージを表示する

### 16.2 失敗動画の再ダウンロード

Failed 状態の動画を再エンキューして再試行できるようにする。

#### 実装内容

**Domain:**
- `Video.ResetToPending(TimeProvider)` メソッドを追加
  - Failed → Pending への状態遷移のみを許可

**Application:**
- `IRetryVideoDownloadUseCase` インターフェースを追加
- `RetryVideoDownloadUseCase` を実装:
  1. 動画を取得（存在しない場合は `InvalidOperationException`）
  2. Failed ステータス確認（Failed でない場合は `InvalidOperationException`）
  3. `video.ResetToPending()` → ステータスを Pending に戻す
  4. `IDownloadQueueService.EnqueueAsync()` で再エンキュー
  5. `IUnitOfWork.SaveChangesAsync()` でコミット
- `DependencyInjection.cs` に DI 登録

**Functions:**
- `POST /api/videos/{id}/retry` エンドポイントを追加

**Frontend:**
- `videoDetail.js`: Failed 状態の動画に「再ダウンロード」ボタンを表示

### 16.3 ダウンロード状態ポーリング

Pending/Downloading/Processing 状態の動画のステータス変化をフロントエンドで自動検知する。

#### 実装内容

**videoDetail.js:**
- ステータスが Pending/Downloading/Processing の場合、3 秒ごとに `GET /api/videos/{id}` をポーリング
- `page.isConnected` を確認してページ離脱時に自動停止
- Ready に変化したらページを再描画してプレイヤーを表示
- Failed に変化したら再試行ボタンを表示して停止

**videoList.js:**
- `fetchAndRender()` の結果に Pending/Downloading/Processing の動画が含まれる場合、5 秒後に自動リフレッシュ
- `pageEl.isConnected` を確認してページ離脱時に自動停止

## 完了条件

- [ ] 同一 TweetUrl の動画を登録しようとすると 409 Conflict が返る
- [ ] Failed 状態の動画詳細ページに「再ダウンロード」ボタンが表示される
- [ ] 再ダウンロードボタンを押すと動画が再エンキューされ、ステータスが Pending に戻る
- [ ] 詳細ページでダウンロード中の動画ステータスが自動更新される
- [ ] 一覧ページでダウンロード中の動画がある場合に自動リフレッシュされる
- [ ] `dotnet test` で全 C# テストが通過する
- [ ] `npm test` で全 JS テストが通過する
- [ ] CLAUDE.md の規約違反がない
