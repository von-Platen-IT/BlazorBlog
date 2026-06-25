using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IPostRatingRepository
{
    Task<PostRating?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default);
    Task<(int LikeCount, int DislikeCount)> GetCountsAsync(Guid postId, CancellationToken ct = default);
    Task<Dictionary<Guid, (int LikeCount, int DislikeCount)>> GetCountsByPostIdsAsync(List<Guid> postIds, CancellationToken ct = default);
    Task<PostRating> AddAsync(PostRating rating, CancellationToken ct = default);
    Task UpdateAsync(PostRating rating, CancellationToken ct = default);
    Task DeleteAsync(PostRating rating, CancellationToken ct = default);
}