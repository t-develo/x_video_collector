# Raspberry Pi でのローカル運用ガイド

Azure を使わず、Raspberry Pi 1 台で X Video Collector を完結運用するための手順書。

- API・フロントエンド配信・動画ダウンロードをすべて 1 つの systemd サービスで動かす
- データベースは SQLite、動画はローカルファイルシステムに保存
- 家庭内 LAN からブラウザでアクセスする（認証なし）

Azure 構成（Functions / Blob Storage / Static Web Apps）はそのまま残っており、
この構成と共存できる。Azure へのデプロイ手順は [deployment.md](deployment.md) を参照。

---

## 1. 構成

```
┌──────────────────────── Raspberry Pi (arm64) ────────────────────────┐
│                                                                      │
│   systemd: xvideocollector.service                                   │
│   ┌────────────────────────────────────────────────────────────┐     │
│   │  XVideoCollector.LocalHost (ASP.NET Core / Kestrel :8080)  │     │
│   │                                                            │     │
│   │   /            → フロントエンド SPA (wwwroot)               │     │
│   │   /api/*       → Video / Tag / Category / Stats / Health    │     │
│   │   /api/media/* → 署名付きメディア配信 (Range 対応)          │     │
│   │                                                            │     │
│   │   DownloadWorker (BackgroundService)                       │     │
│   │     プロセス内キュー + DB 走査で yt-dlp を逐次実行           │     │
│   └────────────────────────────────────────────────────────────┘     │
│            │                          │                              │
│     ┌──────▼───────┐        ┌─────────▼──────────┐                   │
│     │ SQLite        │        │ ローカルファイル    │                   │
│     │ *.db          │        │  media/videos/      │                   │
│     │               │        │  media/thumbnails/  │                   │
│     └──────────────┘        └────────────────────┘                   │
│                                                                      │
│   systemd timer: main 監視による自動更新 (日次)                       │
│                  yt-dlp 週次更新 / DB 日次バックアップ                │
└──────────────────────────────────────────────────────────────────────┘
```

Azure 構成との対応:

| 役割 | Azure | Raspberry Pi |
|------|-------|--------------|
| API ホスト | Azure Functions (Isolated Worker) | `XVideoCollector.LocalHost` (Kestrel) |
| フロント配信 | Static Web Apps | 同一プロセスの静的ファイル配信 |
| データベース | Azure SQL Database | SQLite |
| 動画・サムネイル | Blob Storage | ローカルファイルシステム |
| ストリーミング URL | Blob SAS URL | HMAC 署名付き `/api/media/...` |
| ダウンロード起動 | Storage Queue + Queue Trigger | プロセス内キュー + `BackgroundService` |
| 認証 | Entra ID (SWA 組み込み) | なし（`SKIP_AUTH=true`、LAN 限定） |

`Application` / `Domain` 層は両構成で共通。切り替えは環境変数
`Storage__Provider` と `Queue__Provider` だけで行う。

---

## 2. 前提環境

| 項目 | 要件 |
|------|------|
| 機種 | Raspberry Pi 4 または 5（メモリ 2GB 以上を推奨） |
| OS | **64bit** の Raspberry Pi OS (Bookworm) または Ubuntu Server arm64 |
| ストレージ | microSD でも動くが、動画を貯めるなら外付け SSD/USB を推奨 |
| ネットワーク | 有線 LAN 推奨（ダウンロード中の安定性のため） |

> **32bit OS は非対応。** `install.sh` は `aarch64` 以外を検出するとエラー終了する。
> `uname -m` が `armv7l` の場合は 64bit 版 OS へ入れ替えること。

---

## 3. インストール

```bash
sudo apt update && sudo apt install -y git
git clone https://github.com/t-develo/x_video_collector.git
cd x_video_collector

sudo bash scripts/raspi/install.sh
```

スクリプトが行うこと:

1. アーキテクチャ（aarch64）の検証
2. `ffmpeg` / `ffprobe` / `sqlite3` / `curl` を apt で導入
3. .NET 10 SDK を `/opt/dotnet` に導入（`dotnet-install.sh` 使用）
4. yt-dlp（arm64 スタンドアロン版）を `/usr/local/bin/yt-dlp` に導入
5. システムユーザー `xvc` とディレクトリを作成
6. `/etc/xvideocollector/xvideocollector.env` を生成（**署名キーを自動生成**）
7. `dotnet publish -r linux-arm64` で `/opt/xvideocollector` に発行
8. systemd ユニットを配置し **`systemctl enable --now`（＝再起動時の自動起動を有効化）**
9. `/api/health` をポーリングして疎通確認

### オプション

| オプション | 説明 | 既定 |
|-----------|------|------|
| `--media-path <PATH>` | 動画の保存先 | 既存の設定、無ければ `/var/lib/xvideocollector/media` |
| `--port <PORT>` | 待ち受けポート | 既存の設定、無ければ `8080` |
| `--user <NAME>` | サービス実行ユーザー | 既存の設定、無ければ `xvc` |
| `--skip-deps` | .NET / ffmpeg / yt-dlp の導入をスキップ | — |
| `--no-auto-update` | 自動更新タイマーを有効化しない | （自動更新は既定で有効） |

**再インストール時は、省略したオプションを既存の環境から引き継ぐ。**
保存先は `xvideocollector.env` の `LocalStorage__RootPath` から、実行ユーザーは
導入済みユニットの `User=` から読み戻す。そのため

```bash
sudo bash scripts/raspi/install.sh
```

を素の引数で実行しても、外付けドライブ運用や独自ユーザーの構成は壊れない。
変更したいときだけオプションを明示する。

`/etc/xvideocollector/xvideocollector.env` が既にある場合、設定を失わない
ようファイル全体は上書きしない。ただし `--port` を明示した場合は
`ASPNETCORE_URLS` のポート番号だけを書き換える。
ポート以外の設定を変えるときは env を直接編集する。

```bash
# 8080 で導入済みの環境を 58180 に移す
sudo bash scripts/raspi/install.sh --port 58180
```

外付け SSD に保存する例（先に「7. 外付けドライブ」を実施しておく）:

```bash
sudo bash scripts/raspi/install.sh --media-path /mnt/ssd/xvideocollector
```

指定先が別マウントの場合、install.sh は systemd の drop-in で
`RequiresMountsFor=` を追加し、**マウント前にサービスが起動しないようにする**。

### アクセス

```
http://<ラズパイのIP>:8080
http://<ホスト名>.local:8080     # avahi-daemon が動いている場合
```

---

## 4. 日常の運用

```bash
# 状態確認
sudo systemctl status xvideocollector

# ログ追跡（ダウンロードの進行状況もここに出る）
sudo journalctl --unit=xvideocollector -f

# 直近のエラーだけ
sudo journalctl --unit=xvideocollector -p err -n 50 --no-pager

# 再起動 / 停止
sudo systemctl restart xvideocollector
sudo systemctl stop xvideocollector

# タイマーの状態（自動更新 / yt-dlp 更新 / バックアップ の 3 本）
systemctl list-timers 'xvideocollector*'
```

### 自動起動の確認

```bash
systemctl is-enabled xvideocollector    # → enabled
sudo reboot                              # 再起動後に自動で立ち上がる
```

### 更新

#### 自動更新（既定で有効）

`xvideocollector-update.timer` が **日次で `origin/main` を監視**し、
新しいコミットがあれば自動で取り込んで再発行する。

```
git fetch origin main
  ├─ HEAD と origin/main が同じ  → 何もせず終了（発行は走らない）
  └─ 新しいコミットがある
       ├─ ダウンロード/変換の実行中     → 今回は見送り（翌日の発火で再挑戦）
       ├─ git pull --ff-only
       ├─ dotnet publish → 一時ディレクトリ
       ├─ 旧バージョンを退避してから入れ替え
       ├─ systemd ユニットを同期（差分があるものだけ入れ替え）
       ├─ サービス再起動 → ヘルスチェック
       │    ├─ OK → 完了
       │    └─ NG → 旧バージョンへ自動で戻して再起動
       │            （アプリと systemd ユニットの両方を戻す）
       └─ 完了
```

更新が無い日は `git fetch` とコミットハッシュの比較だけで終わるため、
`dotnet publish` は走らず負荷はほぼ無い。

```bash
# 実行状況の確認
systemctl list-timers 'xvideocollector-update*'
sudo journalctl --unit=xvideocollector-update -n 50 --no-pager

# 今すぐ実行する（タイマーを待たない）
sudo systemctl start xvideocollector-update.service

# 停止 / 再開
sudo systemctl disable --now xvideocollector-update.timer
sudo systemctl enable --now xvideocollector-update.timer
```

インストール時に無効化しておく場合は `sudo bash scripts/raspi/install.sh --no-auto-update`。

**頻度を変える**には drop-in で上書きする（例: 6 時間ごと）:

```bash
sudo systemctl edit xvideocollector-update.timer
```

```ini
[Timer]
# 既定値を消してから設定する
OnCalendar=
OnCalendar=*-*-* 00/6:00:00
```

**自動更新が止まる条件**（いずれも `systemctl status xvideocollector-update` が failed になる）:

| 条件 | 理由 |
|------|------|
| クローンが `main` 以外をチェックアウト中 | 作業ブランチを勝手に切り替えないため |
| コミットしていない変更がある | ローカルの変更を失わないため |
| `--ff-only` でマージできない | 履歴が分岐している。手動での解決が必要 |

#### systemd ユニットの同期

`update.sh`（自動・手動どちらも）は、リポジトリの `scripts/raspi/systemd/` と
導入済みユニットを毎回比較し、**差分があるものだけ入れ替えて `daemon-reload`** する。
ユニットが追加・変更されても `install.sh` を実行し直す必要はない。

導入時に指定した値（実行ユーザー・`dotnet` / `yt-dlp` のパス）は、
導入済みユニットと `xvideocollector.env` から読み戻して描画するため、
`--user` などを指定して導入した環境でも設定が失われない。

いくつか意図的にそうしている点がある:

- **新規に追加されたタイマーだけ `enable` する。**
  意図的に `disable` したタイマーは、更新しても有効化されない
- **`mount.conf` drop-in は同期しない。**
  マウント構成はコード更新で変わるものではなく、`install.sh` の管轄
- **`install.sh` 自体と `xvideocollector.env.example` の変更は反映されない。**
  依存の導入や設定生成を伴うため。差分があった場合はログに警告が出るので、
  内容を見て必要なら `sudo bash scripts/raspi/install.sh` を実行し直す

自動更新・手動更新・`install.sh` は同じロック (`/run/xvideocollector-update.lock`) を取るため、
同時に走って発行先を壊すことはない。自動更新は競合したら見送り、
`install.sh` は競合したらエラーで止まる。

#### 自動更新が入っていない環境からの移行

自動更新タイマーより前に導入した環境では、タイマーがまだ存在しない。
**`update.sh` を 2 回実行**すれば導入される。

```bash
cd ~/x_video_collector
sudo bash scripts/raspi/update.sh   # 1回目: 新しい update.sh を取得する
sudo bash scripts/raspi/update.sh   # 2回目: 新ロジックがユニットを配置する
```

1回目に動いているのは更新前の `update.sh` で、ユニット同期の処理をまだ持たない。
そのため 2 回必要になる（`sudo bash scripts/raspi/install.sh` を 1 回実行しても同じ）。

導入されたことの確認:

```bash
systemctl list-timers 'xvideocollector-update*'
systemctl is-enabled xvideocollector-update.timer   # → enabled
```

#### 手動更新

```bash
cd ~/x_video_collector
sudo bash scripts/raspi/update.sh
```

`git pull` → 再発行 → サービス再起動 → ヘルスチェックまで行う。
設定ファイルとデータは保持される。ローカルの変更を使いたい場合は `--no-pull`。

更新の有無だけを確認したい場合:

```bash
sudo bash scripts/raspi/update.sh --branch main --check-only
```

### アンインストール

```bash
sudo bash scripts/raspi/uninstall.sh           # データは残す
sudo bash scripts/raspi/uninstall.sh --purge   # データも削除（確認あり）
```

---

## 5. 設定リファレンス

設定は `/etc/xvideocollector/xvideocollector.env`。変更後は再起動が必要。

```bash
sudo nano /etc/xvideocollector/xvideocollector.env
sudo systemctl restart xvideocollector
```

| 変数 | 説明 |
|------|------|
| `ASPNETCORE_URLS` | 待ち受けアドレス。既定 `http://0.0.0.0:8080` |
| `TMPDIR` | yt-dlp の作業ディレクトリ。**tmpfs (RAM) を指さないこと**（後述） |
| `SKIP_AUTH` | `true` で認証なし。LAN 限定運用の前提 |
| `Storage__Provider` | `Local` でローカルファイル、`AzureBlob`（既定）で Blob Storage |
| `Queue__Provider` | `InProcess` でプロセス内キュー、`AzureStorageQueue`（既定）で Storage Queue |
| `ConnectionStrings__SqlDb` | `Data Source=...` で始まると SQLite を使用 |
| `LocalStorage__RootPath` | 動画・サムネイルの保存ルート |
| `LocalStorage__SigningKey` | 署名付きメディア URL の鍵（install.sh が自動生成） |
| `LocalStorage__MinimumFreeDiskMB` | この空き容量を下回るとダウンロードを中止（既定 1024） |
| `DownloadWorker__SweepIntervalSeconds` | 未処理動画を DB から拾い直す間隔（既定 300） |
| `DownloadWorker__StaleAfterMinutes` | 実行中のまま滞留した動画を中断扱いにする時間（既定 30） |
| `YtDlp__ExecutablePath` / `FfmpegPath` / `FfprobePath` | 各実行ファイルのパス |
| `YtDlp__TimeoutSeconds` | ダウンロードのタイムアウト（Pi 向けに既定 900） |
| `YtDlp__MaxFileSizeMB` | 1 本あたりの最大サイズ（既定 500） |
| `YtDlp__CookiesPath` | X のログイン cookies（任意、後述） |
| `XVC_BACKUP_RETENTION` | DB バックアップの保持世代数（既定 14） |
| `Logging__LogLevel__Default` | ログレベル。切り分け時は `Debug` |

### TMPDIR について

`YtDlpDownloadService` は `Path.GetTempPath()`（＝`TMPDIR`）配下に動画を
ダウンロードしてから保存先へ移す。Raspberry Pi OS では `/tmp` が tmpfs（RAM）
のことがあり、数百 MB の動画で **OOM killer に落とされる**。
そのため既定で `TMPDIR=/var/lib/xvideocollector/tmp`（実ディスク）を指している。
`LocalStorage__RootPath` を外付けドライブに変えた場合は、`TMPDIR` も
同じドライブに置くと保存時のコピーが減って速い。

### X のログイン cookies

X は多くの動画で認証を要求するため、未設定だとダウンロードが失敗し続けることがある。
ブラウザの拡張機能等で Netscape 形式の `cookies.txt` を書き出し、次のように設置する。

```bash
sudo cp cookies.txt /etc/xvideocollector/cookies.txt
sudo chown root:xvc /etc/xvideocollector/cookies.txt
sudo chmod 640 /etc/xvideocollector/cookies.txt

# env の YtDlp__CookiesPath 行のコメントを外す
sudo nano /etc/xvideocollector/xvideocollector.env
sudo systemctl restart xvideocollector
```

未設定またはファイルが存在しない場合、yt-dlp に `--cookies` は渡されない。

> cookies はアカウントのセッションそのもの。パーミッションを緩めないこと。

---

## 6. バックアップとリストア

動画本体は X から再取得できるが、タグ・カテゴリ・メモは失うと戻らないため
**DB を日次でバックアップ**している（`xvideocollector-backup.timer`）。

```bash
# 保存先
ls -lh /var/lib/xvideocollector/backups/

# 手動で今すぐ取る
sudo systemctl start xvideocollector-backup.service
```

古い世代は `XVC_BACKUP_RETENTION`（既定 14）を超えた分から自動削除される。

### リストア

```bash
sudo systemctl stop xvideocollector
sudo -u xvc cp /var/lib/xvideocollector/backups/xvideocollector-YYYYMMDD-HHMMSS.db \
               /var/lib/xvideocollector/xvideocollector.db
sudo systemctl start xvideocollector
```

動画ファイル自体もバックアップしたい場合は `LocalStorage__RootPath` を
別ドライブや NAS へ rsync する（サイズが大きいため自動化はしていない）。

```bash
rsync -av --delete /var/lib/xvideocollector/media/ /mnt/backup/xvc-media/
```

---

## 7. 外付けドライブ（推奨）

microSD は書き込み寿命が短く、動画の保存先には向かない。

```bash
# 1. UUID を確認
lsblk -f

# 2. マウントポイントを作成
sudo mkdir -p /mnt/ssd

# 3. /etc/fstab に追記（nofail = 未接続でも起動を止めない）
echo 'UUID=<確認したUUID> /mnt/ssd ext4 defaults,nofail,noatime 0 2' | sudo tee -a /etc/fstab
sudo systemctl daemon-reload
sudo mount -a

# 4. このパスを指定してインストール
sudo bash scripts/raspi/install.sh --media-path /mnt/ssd/xvideocollector
```

導入済みの場合は env の `LocalStorage__RootPath` を書き換えてから、
既存メディアを移動して再起動する。

```bash
sudo systemctl stop xvideocollector
sudo rsync -av /var/lib/xvideocollector/media/ /mnt/ssd/xvideocollector/
sudo chown -R xvc:xvc /mnt/ssd/xvideocollector
sudo nano /etc/xvideocollector/xvideocollector.env   # RootPath を変更
sudo systemctl start xvideocollector
```

> パスを変えても DB 内の `BlobPath` は `{container}/{blobName}` の相対形式なので
> 書き換え不要。

---

## 8. ネットワークとセキュリティ

**この構成は認証を掛けていない。** `/api/*` に到達できる相手は誰でも
動画の登録・削除ができるため、ネットワーク側で境界を作ること。

```bash
sudo apt install -y ufw
sudo ufw default deny incoming
sudo ufw allow ssh
sudo ufw allow from 192.168.0.0/16 to any port 8080 proto tcp
sudo ufw enable
sudo ufw status
```

`192.168.0.0/16` は自宅 LAN のサブネットに合わせて調整する。

### ホスト名でのアクセス（mDNS）

```bash
sudo apt install -y avahi-daemon
sudo systemctl enable --now avahi-daemon
# → http://<ホスト名>.local:8080 でアクセスできる（IP 固定が不要になる）
```

### 外部公開する場合

ポート開放は行わず、Tailscale や Cloudflare Tunnel を使うこと。
その場合はプロキシ側で認証を掛けたうえで `SKIP_AUTH=false` にすると、
`X-MS-CLIENT-PRINCIPAL` ヘッダを持つリクエストだけが `/api/*` を通る。

---

## 9. トラブルシューティング

### サービスが起動しない

```bash
sudo systemctl status xvideocollector
sudo journalctl --unit=xvideocollector -n 100 --no-pager
```

よくある原因:

| 症状 | 対処 |
|------|------|
| `Failed to bind to address ...: address already in use` | ポート競合。下の「ポートが競合している場合」を参照 |
| `LocalStorage:RootPath is not configured` | env の `LocalStorage__RootPath` が空。設定して再起動 |
| `LocalStorage:SigningKey is not configured` | env の `LocalStorage__SigningKey` が空。任意のランダム文字列を設定 |
| `Connection string 'SqlDb' is not configured` | env の `ConnectionStrings__SqlDb` が空 |
| 外付けドライブ未マウントで起動しない | 想定動作（`RequiresMountsFor`）。`sudo mount -a` 後に再起動 |

起動に失敗すると `systemctl status` は `activating (auto-restart)` と
`code=exited, status=1`（想定済みの失敗）または `code=killed, signal=ABRT`
（未処理例外での abort）を表示する。後者は systemd がプロセスを殺したのではなく
**アプリ自身が abort した**という意味なので、原因は必ず `journalctl` の
`Unhandled exception.` 以降に出ている。

ポート競合の場合はスタックトレースの前に理由が 1 行で出る。

```
critical: 待ち受けアドレス http://0.0.0.0:8080 をバインドできません。同じポートを他のプロセスが使用しています。...
```

なお、5 分間に 5 回失敗するとユニットは `failed` で停止する
（`StartLimitBurst`）。無限に再起動を繰り返して CPU を消費することはない。

#### ポートが競合している場合

他のプロセスが既定の 8080 を使っていると、Kestrel がバインドできず起動しない。

```bash
# 誰が使っているか調べる
sudo ss -ltnp | grep ':8080'
```

占有プロセスを止めるか、別ポートに変更する。

```bash
# 別ポートへ変更（導入済み・未導入のいずれも同じコマンドでよい）
sudo bash scripts/raspi/install.sh --port 8081

# env を直接編集する場合
sudo sed -i 's|^ASPNETCORE_URLS=.*|ASPNETCORE_URLS=http://0.0.0.0:8081|' \
  /etc/xvideocollector/xvideocollector.env
sudo systemctl restart xvideocollector
```

`install.sh` はポートの空きを **2 回** 確認する。

1. 依存導入・発行を始める前（無駄な数分を使わないため）
2. `systemctl enable --now` の直前

発行には数分かかるため、その間に他のプロセスがポートを取ることがある。2 回目の確認は
このケースを捉えるためのもので、Kestrel のスタックトレースではなく次のように占有プロセス名で止まる。

```
[FAIL]  ポート 8080 は既に使用されています
[FAIL]  占有プロセス:
[FAIL]    1234 root     dotnet /opt/xvideocollector/XVideoCollector.LocalHost.dll
[FAIL]      → systemd 管理外のプロセス（手動起動と思われる）:  sudo kill 1234
```

この時点で発行そのものは完了しているため、ポートを空けたあとは再インストールせずに
起動するだけでよい。

```bash
sudo systemctl enable --now xvideocollector
```

なお、起動に失敗して再起動ループに入った場合、`install.sh` / `update.sh` は診断ログを
出したうえでサービスを停止する（自動起動の設定は残る）。原因を解消したら
`sudo systemctl start xvideocollector` で起動する。

### ダウンロードが失敗する

まず `/api/health` と一覧画面の失敗理由を確認する。

```bash
curl -s http://localhost:8080/api/health | python3 -m json.tool
```

| `checks` の項目 | Unhealthy の意味 |
|-----------------|-----------------|
| `sql` | DB に接続できない（ファイル権限やディスク full） |
| `blob` | メディアルートが存在しない／書き込めない |
| `ytdlp` / `ffmpeg` / `ffprobe` | 実行ファイルのパスが誤っている |
| `disk` | 空き容量不足。この状態では新規ダウンロードを中止する |

yt-dlp 側の失敗は X の仕様変更が原因のことが多い。

```bash
# 手動で更新（週次タイマーもあるが即時に試したいとき）
sudo /usr/local/bin/yt-dlp -U

# 単体で試して原因を見る
sudo -u xvc /usr/local/bin/yt-dlp -F "https://x.com/<user>/status/<id>"
```

`Sign in to confirm` 等が出る場合は「5. X のログイン cookies」を設定する。

### ダウンロード中にプロセスが落ちる

`TMPDIR` が tmpfs（RAM）を指していないか確認する。

```bash
df -h /var/lib/xvideocollector/tmp     # tmpfs なら NG
journalctl -k | grep -i "out of memory"
```

### ディスクが一杯になった

```bash
df -h /var/lib/xvideocollector
du -sh /var/lib/xvideocollector/media/*
```

不要な動画を UI から削除するか、`LocalStorage__RootPath` を大容量ドライブへ移す。
空き容量が `LocalStorage__MinimumFreeDiskMB` を下回っている間、
新規ダウンロードは「ディスクの空き容量が不足しています」という失敗理由で中止される
（容量を確保してから UI の再試行で復帰できる）。

### サービス停止中に登録した動画が処理されない

`DownloadWorker` が起動時と定期（既定 5 分）に DB を走査して、
`Pending` の動画と、`Downloading` / `Processing` のまま中断された動画を拾い直す。
`journalctl` に「未処理の動画を N 件キューに投入しました」が出れば動作している。

---

## 10. 既知の制約

- **SQLite 用の EF マイグレーションが無い。**
  SQLite 経路は起動時の `EnsureCreated` でスキーマを作るだけで、Migrations は
  SQL Server 専用。アプリを更新してエンティティのスキーマが変わった場合、
  既存 DB は自動更新されない。その際は DB をバックアップしたうえで作り直すか、
  手動で `ALTER TABLE` する必要がある（動画ファイル自体は残るが、
  メタデータは再登録になる）。
- **重複判定は正規化済み URL の完全一致**。
  同じツイートを異なる大文字小文字のユーザー名で登録すると別物として扱われる。
- **同時ダウンロードは 1 件**。Pi の CPU/メモリを保護するための固定値。
- Azure 構成側の既知の不具合として、`infra/modules/functions.bicep` が指す
  `ffprobe.exe` がデプロイパッケージに同梱されていない（ラズパイ運用には影響しない）。

---

## 関連ドキュメント

| ドキュメント | 内容 |
|-------------|------|
| [../README.md](../README.md) | プロジェクト概要 |
| [deployment.md](deployment.md) | Azure へのデプロイ手順 |
| [../CLAUDE.md](../CLAUDE.md) | コーディング規約・アーキテクチャ規約 |
