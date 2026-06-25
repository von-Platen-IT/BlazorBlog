using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class PostRatingRepository(BlogDbContext db) : IPostRatingRepository
{
    public async Task<PostRating?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default) =>
        await db.PostRatings.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

    public async Task<(int LikeCount, int DislikeCount)> GetCountsAsync(Guid postId, CancellationToken ct = default)
    {
        var ratings = await db.PostRatings.Where(r => r.PostId == postId).ToListAsync(ct);
        return (ratings.Count(r => r.IsLike), ratings.Count(r => !r.IsLike));
    }

    public async Task<Dictionary<Guid, (int LikeCount, int DislikeCount)>> GetCountsByPostIdsAsync(List<Guid> postIds, CancellationToken ct = default)
    {
        if (postIds.Count == 0) return new Dictionary<Guid, (int, int)>();

        var ratings = await db.PostRatings
            .Where(r => postIds.Contains(r.PostId))
            .GroupBy(r => r.PostId)
            .Select(g => new
            {
                PostId = g.Key,
                LikeCount = g.Count(r => r.IsLike),
                DislikeCount = g.Count(r => !r.IsLike)
            })
            .ToListAsync(ct);

        return ratings.ToDictionary(r => r.PostId, r => (r.LikeCount, r.DislikeCount));
    }

    public async Task<PostRating> AddAsync(PostRating rating, CancellationToken ct = default)
    {
        db.PostRatings.Add(rating);
        await db.SaveChangesAsync(ct);
        return rating;
    }

    public async Task UpdateAsync(PostRating rating, CancellationToken ct = default)
    {
        db.PostRatings.Update(rating);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(PostRating rating, CancellationToken ct = default)
    {
        db.PostRatings.Remove(rating);
        await db.SaveChangesAsync(ct);
    }
}