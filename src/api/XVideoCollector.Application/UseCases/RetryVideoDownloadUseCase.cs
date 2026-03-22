using XVideoCollector.Application.Exceptions;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class RetryVideoDownloadUseCase(
    IVideoRepository videoRepository,
    IDownloadQueueService downloadQueue,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRetryVideoDownloadUseCase
{
    public async Task ExecuteAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken)
            ?? throw new VideoNotFoundException(videoId);

        if (video.Status != VideoStatus.Failed)
            throw new InvalidOperationException(
                $"Cannot retry a video with status '{video.Status}'. Only Failed videos can be retried.");

        video.ResetToPending(timeProvider);
        await videoRepository.UpdateAsync(video, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await downloadQueue.EnqueueAsync(videoId, cancellationToken);
    }
}
