using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Infrastructure.Persistence.Configurations;

internal sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("Videos");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .ValueGeneratedNever();

        builder.Property(v => v.TweetUrl)
            .HasMaxLength(512)
            .HasConversion(
                v => v.Value,
                v => TweetUrl.Create(v))
            .IsRequired();

        // Title は複合型としてマッピングする。
        // 値コンバーターでは v.Title.Value が SQL に変換できず、
        // キーワード検索 (LIKE) やタイトル並べ替えがリレーショナルプロバイダーで失敗するため。
        // 列名は "Title" のままなのでスキーマは変わらない。
        builder.ComplexProperty(v => v.Title, title =>
        {
            title.Property(t => t.Value)
                .HasColumnName("Title")
                .HasMaxLength(VideoTitle.MaxLength)
                .IsRequired();
        });

        builder.Property(v => v.Status)
            .HasConversion(new EnumToStringConverter<VideoStatus>())
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(v => v.BlobPath)
            .HasMaxLength(1024)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => v == null ? null : BlobPath.Create(v));

        builder.Property(v => v.ThumbnailBlobPath)
            .HasMaxLength(1024)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => v == null ? null : BlobPath.Create(v));

        builder.Property(v => v.DurationSeconds);
        builder.Property(v => v.FileSizeBytes);
        builder.Property(v => v.CategoryId);

        builder.Property(v => v.Notes)
            .HasMaxLength(Video.NotesMaxLength);

        builder.Property(v => v.FailureReason)
            .HasMaxLength(Video.FailureReasonMaxLength);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .IsRequired();

        builder.HasIndex(v => v.TweetUrl).IsUnique();
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.CategoryId);
        builder.HasIndex(v => v.CreatedAt);
    }
}
