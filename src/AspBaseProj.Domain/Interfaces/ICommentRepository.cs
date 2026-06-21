using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Comment>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);
    Task<List<Comment>> GetPendingAsync(CancellationToken ct = default);
    Task<Comment> AddAsync(Comment comment, CancellationToken ct = default);
    Task UpdateAsync(Comment comment, CancellationToken ct = default);
    Task DeleteAsync(Comment comment, CancellationToken ct = default);
}