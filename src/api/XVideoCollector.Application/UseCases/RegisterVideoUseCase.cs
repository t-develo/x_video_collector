using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.UseCases;

public sealed class RegisterVideoUseCase(
    IVideoRepository videoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRegisterVideoUseCase
{
    public async Task<VideoDto> ExecuteAsync(
        RegisterVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tweetUrl = TweetUrl.Create(request.TweetUrl);

        var existing = await videoRepository.FindByTweetUrlAsync(tweetUrl, cancellationToken);
        if (existing is not null)
            throw new DuplicateTweetUrlException(tweetUrl.TweetId);

        var title = VideoTitle.Create(
            string.IsNullOrWhiteSpace(request.Title)
                ? BuildFallbackTitle(tweetUrl)
                : request.Title);
        var video = Video.Create(tweetUrl, title, timeProvider);

        await videoRepository.AddAsync(video, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return VideoMapper.ToDto(video, []);
    }

    /// <summary>
    /// タイトル未指定時の暫定タイトルを URL から生成する。
    /// ダウンロード後にユーザーが編集する前提の識別用ラベル。
    /// </summary>
    private static string BuildFallbackTitle(TweetUrl tweetUrl) =>
        $"@{tweetUrl.UserName} - {tweetUrl.TweetId}";
}
