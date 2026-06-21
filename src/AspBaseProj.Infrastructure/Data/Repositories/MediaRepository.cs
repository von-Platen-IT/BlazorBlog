using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class MediaRepository(BlogDbContext db) : IMediaRepository
{
    public async Task<Media?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Media.FindAsync([id], ct);

    public async Task<List<Media>> GetByPostIdAsync(Guid postId, CancellationToken ct = default) =>
        await db.Media.Where(m => m.PostId == postId).AsNoTracking().ToListAsync(ct);

    public async Task<Media> AddAsync(Media media, CancellationToken ct = default)
    {
        db.Media.Add(media);
        await db.SaveChangesAsync(ct);
        return media;
    }

    public async Task DeleteAsync(Media media, CancellationToken ct = default)
    {
        db.Media.Remove(media);
        await db.SaveChangesAsync(ct);
    }
}