using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class GroupRepository(BlogDbContext db) : IGroupRepository
{
    public async Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Groups.FindAsync([id], ct);

    public async Task<Group?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await db.Groups.FirstOrDefaultAsync(g => g.Name == name, ct);

    public async Task<List<Group>> GetAllAsync(CancellationToken ct = default) =>
        await db.Groups.AsNoTracking().ToListAsync(ct);
}