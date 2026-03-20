using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.UseCases;

public class RegisterVideoUseCase(
    IVideoRepository videoRepository)
{
    public virtual async Task<VideoDto> ExecuteAsync(
        RegisterVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tweetUrl = TweetUrl.Create(request.TweetUrl);
        var title = VideoTitle.Create(request.Title);
        var video = Video.Create(tweetUrl, title);

        await videoRepository.AddAsync(video, cancellationToken);

        return VideoMapper.ToDto(video, []);
    }
}
