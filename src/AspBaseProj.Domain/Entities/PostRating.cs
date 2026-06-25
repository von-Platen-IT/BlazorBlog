namespace AspBaseProj.Domain.Entities;

public class PostRating
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public bool IsLike { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Post Post { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}