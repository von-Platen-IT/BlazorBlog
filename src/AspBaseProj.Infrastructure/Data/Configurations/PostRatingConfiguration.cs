using AspBaseProj.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspBaseProj.Infrastructure.Data.Configurations;

public class PostRatingConfiguration : IEntityTypeConfiguration<PostRating>
{
    public void Configure(EntityTypeBuilder<PostRating> builder)
    {
        builder.ToTable("PostRatings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.PostId);

        builder.HasOne(x => x.Post)
            .WithMany(p => p.Ratings)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}