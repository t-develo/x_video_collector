# Sessions

このディレクトリは iOS Claude Code のセッション間でコンテキストを保持するためのセッション記録を保存します。

## ファイル命名規則

```
YYYY-MM-DD-<short-description>-session.md
```

例:
- `2026-03-22-domain-entities-session.md`
- `2026-03-23-video-usecase-session.md`

## 使い方

### セッション保存
セッション終了前に `/save-session` を実行して現在の作業コンテキストを保存する。

### セッション復元
次のセッション開始時に `/resume-session` を実行して前回のコンテキストを復元する。
または、session-start フックが自動的に最新セッションのサマリーを表示する。

## iOS での注意事項

セッションファイルはこのリポジトリに保存されますが、**自動的にはコミットされません**。
セッション保存後、以下を実行してください：

```bash
git add .claude/sessions/
git commit -m "chore: save session YYYY-MM-DD"
git push
```

これにより次回 iOS でリポジトリを開いた際にセッション記録が参照できます。
