using System.Threading.Channels;

namespace XVideoCollector.Infrastructure.Services;

/// <summary>
/// プロセス内ダウンロードキュー。
/// Azure Storage Queue を使わないスタンドアロン運用（Raspberry Pi 等）で、
/// API 側の登録処理と常駐ワーカーを繋ぐ。
/// プロセス再起動でキュー内容は失われるため、ワーカー側で DB を走査して
/// 未処理の動画を拾い直す運用と組み合わせて使用する。
/// </summary>
public sealed class DownloadQueueChannel
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelWriter<Guid> Writer => _channel.Writer;

    public ChannelReader<Guid> Reader => _channel.Reader;
}
