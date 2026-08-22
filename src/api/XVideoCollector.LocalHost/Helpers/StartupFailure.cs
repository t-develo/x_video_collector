using Microsoft.AspNetCore.Connections;

namespace XVideoCollector.LocalHost.Helpers;

/// <summary>
/// 起動時例外のうち、原因と対処が定型的なものを人が読める説明に変換する。
/// systemd 上ではスタックトレースがそのまま journal に流れて原因が埋もれるため、
/// 分かる範囲は 1 行の説明として出す。
/// </summary>
internal static class StartupFailure
{
    /// <summary>
    /// 待ち受けアドレスのバインド失敗（ポート競合）であれば説明文を返す。
    /// それ以外の例外では <c>null</c> を返す（呼び出し側でそのまま送出する）。
    /// </summary>
    /// <param name="exception">ホスト起動時に送出された例外。</param>
    /// <param name="urls">設定されていた待ち受けアドレス（未設定なら <c>null</c>）。</param>
    internal static string? DescribeAddressInUse(Exception exception, string? urls)
    {
        if (!ContainsAddressInUse(exception))
            return null;

        var target = string.IsNullOrWhiteSpace(urls) ? "既定の待ち受けアドレス" : urls;

        return $"待ち受けアドレス {target} をバインドできません。" +
               "同じポートを他のプロセスが使用しています。" +
               "占有プロセスは `sudo ss -ltnp | grep ':<ポート>'` で確認できます。" +
               "別のポートで動かす場合は /etc/xvideocollector/xvideocollector.env の " +
               "ASPNETCORE_URLS を変更して `sudo systemctl restart xvideocollector` を実行してください。";
    }

    private static bool ContainsAddressInUse(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is AddressInUseException)
                return true;
        }

        return false;
    }
}
