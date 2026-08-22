using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using XVideoCollector.LocalHost.Helpers;

namespace XVideoCollector.LocalHost.Tests;

/// <summary>
/// 起動失敗メッセージの変換テスト。
/// ポート競合時に journal へ「何が起きたか」が 1 行で残ることを保証する。
/// </summary>
public sealed class StartupFailureTests
{
    /// <summary>Kestrel が実際に送出する入れ子構造（IOException → AddressInUseException → SocketException）。</summary>
    private static IOException CreateBindFailure() =>
        new(
            "Failed to bind to address http://0.0.0.0:8080: address already in use.",
            new AddressInUseException(
                "Address already in use",
                new SocketException((int)SocketError.AddressAlreadyInUse)));

    [Fact]
    public void DescribeAddressInUse_WithNestedAddressInUseException_ReturnsExplanation()
    {
        var exception = CreateBindFailure();

        var message = StartupFailure.DescribeAddressInUse(exception, "http://0.0.0.0:8080");

        Assert.NotNull(message);
        Assert.Contains("http://0.0.0.0:8080", message);
        Assert.Contains("ASPNETCORE_URLS", message);
    }

    [Fact]
    public void DescribeAddressInUse_WithoutConfiguredUrls_FallsBackToDefaultWording()
    {
        var exception = CreateBindFailure();

        var message = StartupFailure.DescribeAddressInUse(exception, null);

        Assert.NotNull(message);
        Assert.Contains("既定の待ち受けアドレス", message);
    }

    [Fact]
    public void DescribeAddressInUse_WithBlankConfiguredUrls_FallsBackToDefaultWording()
    {
        var exception = CreateBindFailure();

        var message = StartupFailure.DescribeAddressInUse(exception, "   ");

        Assert.NotNull(message);
        Assert.Contains("既定の待ち受けアドレス", message);
    }

    [Fact]
    public void DescribeAddressInUse_WithUnrelatedException_ReturnsNull()
    {
        var exception = new InvalidOperationException(
            "Connection string 'SqlDb' is not configured",
            new IOException("disk failure"));

        var message = StartupFailure.DescribeAddressInUse(exception, "http://0.0.0.0:8080");

        Assert.Null(message);
    }

    [Fact]
    public void DescribeAddressInUse_WithAddressInUseAtTopLevel_ReturnsExplanation()
    {
        var exception = new AddressInUseException("Address already in use");

        var message = StartupFailure.DescribeAddressInUse(exception, "http://0.0.0.0:8080");

        Assert.NotNull(message);
    }
}
