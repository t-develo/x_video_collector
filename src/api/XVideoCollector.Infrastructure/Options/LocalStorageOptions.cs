namespace XVideoCollector.Infrastructure.Options;

/// <summary>
/// ローカルファイルシステムにメディアを保存する場合の設定
/// （Raspberry Pi 等のスタンドアロン運用向け）。
/// </summary>
public sealed class LocalStorageOptions
{
    public const string SectionName = "LocalStorage";

    /// <summary>メディアファイルの保存ルートディレクトリ。</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>署名付きメディア URL の HMAC 署名キー。</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>署名付きメディア URL のパス接頭辞。</summary>
    public string MediaPathBase { get; set; } = "/api/media";

    /// <summary>
    /// 新規ダウンロードを許可する最小空き容量 (MB)。
    /// これを下回るとダウンロードを中止し、ヘルスチェックも Unhealthy になる。
    /// </summary>
    public long MinimumFreeDiskMB { get; set; } = 1024;
}
