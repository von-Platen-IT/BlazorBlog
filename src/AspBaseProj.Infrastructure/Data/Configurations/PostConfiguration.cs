using AspBaseProj.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspBaseProj.Infrastructure.Data.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Content).IsRequired().HasColumnType("text");

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.AuthorId);
    }
}