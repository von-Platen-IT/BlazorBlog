using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class AppUserRepository(BlogDbContext db) : IAppUserRepository
{
    public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.AppUsers.Include(u => u.Groups).Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken ct = default) =>
        await db.AppUsers.Include(u => u.Groups).Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.UserName == userName, ct);

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await db.AppUsers.Include(u => u.Groups)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<List<AppUser>> GetAllAsync(CancellationToken ct = default) =>
        await db.AppUsers.Include(u => u.Groups).AsNoTracking().ToListAsync(ct);

    public async Task<AppUser> AddAsync(AppUser user, CancellationToken ct = default)
    {
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        db.AppUsers.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(AppUser user, CancellationToken ct = default)
    {
        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(ct);
    }
}