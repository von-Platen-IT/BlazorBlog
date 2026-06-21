using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IExternalLoginRepository
{
    Task<ExternalLogin?> GetByProviderAsync(string provider, string providerKey, CancellationToken ct = default);
    Task<List<ExternalLogin>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<ExternalLogin> AddAsync(ExternalLogin externalLogin, CancellationToken ct = default);
    Task DeleteAsync(ExternalLogin externalLogin, CancellationToken ct = default);
}