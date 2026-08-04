using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Domain.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Video>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Video> Videos, int TotalCount)> GetPagedAsync(int skip, int take, VideoSortOrder sortOrder = VideoSortOrder.CreatedAtDesc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Video>> SearchAsync(VideoSearchQuery query, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Video> Videos, int TotalCount)> SearchPagedAsync(VideoSearchQuery query, int skip, int take, CancellationToken cancellationToken = default);
    /// <summary>
    /// 正規化済みのツイート URL で完全一致検索する（重複登録の判定に使用）。
    /// </summary>
    Task<Video?> FindByTweetUrlAsync(TweetUrl tweetUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したステータスの動画を、最終更新が <paramref name="updatedBefore"/> より前のものに絞って取得する。
    /// 常駐ワーカーが未処理・中断された動画を拾い直すために使用する。
    /// </summary>
    Task<IReadOnlyList<Video>> GetByStatusesAsync(
        IReadOnlyCollection<VideoStatus> statuses,
        DateTimeOffset updatedBefore,
        CancellationToken cancellationToken = default);
    Task<VideoStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Video video, CancellationToken cancellationToken = default);
    Task UpdateAsync(Video video, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
