namespace AspBaseProj.Application.Contracts.Auth;

public record LoginRequest(string UserName, string Password);

public record RegisterRequest(string UserName, string? Email, string Password);

public record AuthResponse(Guid UserId, string UserName, string? Email, bool IsRoot, List<string> Groups, string Token);

public record AuthUserInfo(string UserId, string UserName, string? Email, bool IsRoot, List<string> Groups);