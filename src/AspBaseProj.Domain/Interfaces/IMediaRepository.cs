using AspBaseProj.Domain.Entities;

namespace AspBaseProj.Domain.Interfaces;

public interface IMediaRepository
{
    Task<Media?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Media>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);
    Task<Media> AddAsync(Media media, CancellationToken ct = default);
    Task DeleteAsync(Media media, CancellationToken ct = default);
}