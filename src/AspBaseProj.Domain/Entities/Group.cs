namespace AspBaseProj.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}