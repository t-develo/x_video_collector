namespace XVideoCollector.Application.Dtos;

/// <summary>
/// 動画登録リクエスト。
/// </summary>
/// <param name="TweetUrl">X (Twitter) の投稿 URL。</param>
/// <param name="Title">
/// 動画タイトル。省略・空文字の場合は URL から暫定タイトルを自動生成する。
/// </param>
public sealed record RegisterVideoRequest(
    string TweetUrl,
    string? Title = null);
