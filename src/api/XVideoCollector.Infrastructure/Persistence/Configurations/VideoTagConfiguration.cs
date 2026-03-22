using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XVideoCollector.Domain.Entities;

namespace XVideoCollector.Infrastructure.Persistence.Configurations;

internal sealed class VideoTagConfiguration : IEntityTypeConfiguration<VideoTag>
{
    public void Configure(EntityTypeBuilder<VideoTag> builder)
    {
        builder.ToTable("VideoTags");
        builder.HasKey(vt => new { vt.VideoId, vt.TagId });

        builder.Property(vt => vt.VideoId).IsRequired();
        builder.Property(vt => vt.TagId).IsRequired();

        builder.HasOne<Video>()
            .WithMany()
            .HasForeignKey(vt => vt.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(vt => vt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(vt => vt.VideoId);
        builder.HasIndex(vt => vt.TagId);
    }
}
