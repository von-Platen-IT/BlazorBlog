using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<Post> Posts, int TotalCount)> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<List<Post>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default);
    Task<Post> AddAsync(Post post, CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task DeleteAsync(Post post, CancellationToken ct = default);
}