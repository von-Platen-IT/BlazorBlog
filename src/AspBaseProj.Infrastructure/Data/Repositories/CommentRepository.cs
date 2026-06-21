using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class CommentRepository(BlogDbContext db) : ICommentRepository
{
    public async Task<Comment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Comments.Include(c => c.User).Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<Comment>> GetByPostIdAsync(Guid postId, CancellationToken ct = default) =>
        await db.Comments.Where(c => c.PostId == postId && c.IsApproved && c.ParentCommentId == null)
            .Include(c => c.User).Include(c => c.Replies).ThenInclude(r => r.User)
            .OrderBy(c => c.CreatedAt).AsNoTracking().ToListAsync(ct);

    public async Task<List<Comment>> GetPendingAsync(CancellationToken ct = default) =>
        await db.Comments.Where(c => !c.IsApproved)
            .Include(c => c.Post).OrderBy(c => c.CreatedAt).AsNoTracking().ToListAsync(ct);

    public async Task<Comment> AddAsync(Comment comment, CancellationToken ct = default)
    {
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return comment;
    }

    public async Task UpdateAsync(Comment comment, CancellationToken ct = default)
    {
        db.Comments.Update(comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Comment comment, CancellationToken ct = default)
    {
        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }
}