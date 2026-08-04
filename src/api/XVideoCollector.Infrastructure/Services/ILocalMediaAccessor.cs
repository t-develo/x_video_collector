namespace XVideoCollector.Infrastructure.Services;

/// <summary>
/// ローカルファイルシステム上のメディアを HTTP 配信するためのアクセサ。
/// 署名付き URL の検証と物理パス解決を提供する。
/// ローカルストレージ運用時のみ DI に登録される。
/// </summary>
public interface ILocalMediaAccessor
{
    /// <summary>
    /// BlobPath ("{container}/{blobName}") を物理パスに解決する。
    /// ルートディレクトリ外を指す場合やファイルが存在しない場合は false を返す。
    /// </summary>
    bool TryResolvePhysicalPath(string blobPath, out string physicalPath);

    /// <summary>
    /// 署名付き URL のクエリパラメータ (exp, sig) を検証する。
    /// </summary>
    bool ValidateSignature(string blobPath, string? expires, string? signature);

    /// <summary>
    /// メディアルートが存在するドライブの空き容量 (バイト) を返す。
    /// </summary>
    long GetAvailableFreeSpaceBytes();

    /// <summary>
    /// 新規ダウンロードを許可する空き容量が残っているかを返す。
    /// </summary>
    bool HasSufficientFreeSpace();

    /// <summary>新規ダウンロードを許可する最小空き容量 (バイト)。</summary>
    long MinimumFreeSpaceBytes { get; }
}
