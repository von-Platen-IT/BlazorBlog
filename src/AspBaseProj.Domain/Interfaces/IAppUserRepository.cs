using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<List<AppUser>> GetAllAsync(CancellationToken ct = default);
    Task<AppUser> AddAsync(AppUser user, CancellationToken ct = default);
    Task UpdateAsync(AppUser user, CancellationToken ct = default);
    Task DeleteAsync(AppUser user, CancellationToken ct = default);
}