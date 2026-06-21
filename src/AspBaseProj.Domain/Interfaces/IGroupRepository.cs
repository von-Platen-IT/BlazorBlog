using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Group?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<Group>> GetAllAsync(CancellationToken ct = default);
}