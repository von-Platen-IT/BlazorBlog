using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class PostRepository(BlogDbContext db) : IPostRepository
{
    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Posts.Include(p => p.Author).Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(List<Post> Posts, int TotalCount)> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Posts.Where(p => p.IsPublished).Include(p => p.Author);
        var totalCount = await query.CountAsync(ct);
        var posts = await query.OrderByDescending(p => p.PublishedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(ct);
        return (posts, totalCount);
    }

    public async Task<List<Post>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default) =>
        await db.Posts.Where(p => p.AuthorId == authorId).Include(p => p.Author)
            .OrderByDescending(p => p.CreatedAt).AsNoTracking().ToListAsync(ct);

    public async Task<(List<Post> Posts, int TotalCount)> GetByAuthorIdPaginatedAsync(Guid authorId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Posts.Where(p => p.AuthorId == authorId).Include(p => p.Author);
        var totalCount = await query.CountAsync(ct);
        var posts = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(ct);
        return (posts, totalCount);
    }

    public async Task<Post> AddAsync(Post post, CancellationToken ct = default)
    {
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return post;
    }

    public async Task UpdateAsync(Post post, CancellationToken ct = default)
    {
        db.Posts.Update(post);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Post post, CancellationToken ct = default)
    {
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
    }
}