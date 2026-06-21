using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class ExternalLoginRepository(BlogDbContext db) : IExternalLoginRepository
{
    public async Task<ExternalLogin?> GetByProviderAsync(string provider, string providerKey, CancellationToken ct = default) =>
        await db.ExternalLogins.Include(x => x.User).ThenInclude(u => u.Groups)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderKey == providerKey, ct);

    public async Task<List<ExternalLogin>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await db.ExternalLogins.Where(x => x.UserId == userId).AsNoTracking().ToListAsync(ct);

    public async Task<ExternalLogin> AddAsync(ExternalLogin externalLogin, CancellationToken ct = default)
    {
        db.ExternalLogins.Add(externalLogin);
        await db.SaveChangesAsync(ct);
        return externalLogin;
    }

    public async Task DeleteAsync(ExternalLogin externalLogin, CancellationToken ct = default)
    {
        db.ExternalLogins.Remove(externalLogin);
        await db.SaveChangesAsync(ct);
    }
}