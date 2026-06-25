using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IBookmarkRepository
{
    Task<Bookmark?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default);
    Task<(List<Post> Posts, int TotalCount)> GetBookmarkedPostsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<Bookmark> AddAsync(Bookmark bookmark, CancellationToken ct = default);
    Task DeleteAsync(Bookmark bookmark, CancellationToken ct = default);
}