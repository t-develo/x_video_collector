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

        var existing = await videoRepository.FindByTweetIdAsync(tweetUrl.TweetId, cancellationToken);
        if (existing is not null)
            throw new DuplicateTweetUrlException(tweetUrl.TweetId);

        var title = VideoTitle.Create(request.Title);
        var video = Video.Create(tweetUrl, title, timeProvider);

        await videoRepository.AddAsync(video, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return VideoMapper.ToDto(video, []);
    }
}
