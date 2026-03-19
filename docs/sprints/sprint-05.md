# Sprint 5: yt-dlp & ffmpeg Integration

## 目的

yt-dlp と ffmpeg を Process.Start で呼び出す動画ダウンロードサービスの実装。

## ブランチ

`feature/sprint5/ytdlp-ffmpeg`

## タスク

### 5.1 YtDlpOptions 設定クラス

- 実行パス、タイムアウト、最大ファイルサイズの設定

### 5.2 YtDlpDownloadService 実装

- `IVideoDownloadService` の実装
- プロセス起動・管理
- stdout/stderr キャプチャ
- タイムアウト処理
- 進捗レポート

### 5.3 セキュリティ対策

- URL バリデーション（ホワイトリスト）
- コマンドインジェクション防止
- ファイルサイズ制限

### 5.4 サムネイル処理

- yt-dlp の `--write-thumbnail` 出力の処理
- サムネイル変換（ffmpeg 連携）

### 5.5 DownloadVideoUseCase 統合

- ダウンロード → Blob アップロード → DB 更新のフロー
- エラーハンドリング（リトライ不要、ステータスを Failed に更新）

### 5.6 テスト

- URL バリデーションのユニットテスト
- プロセス実行のモックテスト
- タイムアウト処理のテスト

## 完了条件

- [ ] YtDlpDownloadService が実装されている
- [ ] URL バリデーションが正しく機能する
- [ ] タイムアウト処理が正しく機能する
- [ ] ダウンロード → アップロードフローが実装されている
- [ ] セキュリティ要件を満たしている
- [ ] テストがすべてパスする
