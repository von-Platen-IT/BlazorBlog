using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AspBaseProj.Infrastructure.Data.Repositories;

public class SystemSettingRepository(BlogDbContext db) : ISystemSettingRepository
{
    public async Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task<List<SystemSetting>> GetAllAsync(CancellationToken ct = default) =>
        await db.SystemSettings.AsNoTracking().ToListAsync(ct);

    public async Task<SystemSetting> SetAsync(SystemSetting setting, CancellationToken ct = default)
    {
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == setting.Key, ct);
        if (existing is not null)
        {
            existing.Value = setting.Value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            setting.Id = Guid.NewGuid();
            setting.UpdatedAt = DateTime.UtcNow;
            db.SystemSettings.Add(setting);
        }
        await db.SaveChangesAsync(ct);
        return existing ?? setting;
    }
}