using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<List<SystemSetting>> GetAllAsync(CancellationToken ct = default);
    Task<SystemSetting> SetAsync(SystemSetting setting, CancellationToken ct = default);
}