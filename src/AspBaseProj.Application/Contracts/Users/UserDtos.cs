namespace AspBaseProj.Application.Contracts.Users;

public record UserDto(Guid Id, string UserName, string? Email, bool IsRoot, DateTime CreatedAt, List<string> Groups);

public record GroupDto(Guid Id, string Name, string? Description);