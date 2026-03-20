using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.UseCases;

public sealed class RegisterVideoUseCase(
    IVideoRepository videoRepository) : IRegisterVideoUseCase
{
    public async Task<VideoDto> ExecuteAsync(
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
