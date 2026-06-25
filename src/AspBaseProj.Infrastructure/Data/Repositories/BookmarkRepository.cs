using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class BookmarkRepository(BlogDbContext db) : IBookmarkRepository
{
    public async Task<Bookmark?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default) =>
        await db.Bookmarks.FirstOrDefaultAsync(b => b.PostId == postId && b.UserId == userId, ct);

    public async Task<(List<Post> Posts, int TotalCount)> GetBookmarkedPostsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Bookmarks
            .Where(b => b.UserId == userId)
            .Include(b => b.Post).ThenInclude(p => p.Author)
            .OrderByDescending(b => b.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var bookmarks = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking().ToListAsync(ct);

        var posts = bookmarks.Select(b => b.Post).ToList();
        return (posts, totalCount);
    }

    public async Task<Bookmark> AddAsync(Bookmark bookmark, CancellationToken ct = default)
    {
        db.Bookmarks.Add(bookmark);
        await db.SaveChangesAsync(ct);
        return bookmark;
    }

    public async Task DeleteAsync(Bookmark bookmark, CancellationToken ct = default)
    {
        db.Bookmarks.Remove(bookmark);
        await db.SaveChangesAsync(ct);
    }
}