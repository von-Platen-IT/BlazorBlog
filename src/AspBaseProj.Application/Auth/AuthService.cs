using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AspBaseProj.Application.Contracts.Auth;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AspBaseProj.Application.Auth;

public class AuthService(
    IAppUserRepository userRepo,
    IGroupRepository groupRepo,
    IConfiguration configuration,
    ILogger<AuthService> logger)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await userRepo.GetByUserNameAsync(request.UserName);
        if (existing is not null)
            throw new InvalidOperationException("Username already exists.");

        var viewerGroup = await groupRepo.GetByNameAsync("Viewer")
            ?? throw new InvalidOperationException("Viewer group not found.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsRoot = false,
            CreatedAt = DateTime.UtcNow
        };
        user.Groups.Add(viewerGroup);

        await userRepo.AddAsync(user);
        logger.LogInformation("User {UserName} registered", user.UserName);

        var token = GenerateToken(user);
        return MapToResponse(user, token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByUserNameAsync(request.UserName)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("This account uses social login. Please sign in with your provider.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        logger.LogInformation("User {UserName} logged in", user.UserName);

        var token = GenerateToken(user);
        return MapToResponse(user, token);
    }

    public string GenerateToken(AppUser user)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new("is_root", user.IsRoot.ToString().ToLower())
        };

        if (user.Email is not null)
            claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));

        foreach (var group in user.Groups)
            claims.Add(new Claim("groups", group.Name));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthResponse MapToResponse(AppUser user, string token) =>
        new(user.Id, user.UserName, user.Email, user.IsRoot,
            user.Groups.Select(g => g.Name).ToList(), token);
}