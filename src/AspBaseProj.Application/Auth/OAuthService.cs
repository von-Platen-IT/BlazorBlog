using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AspBaseProj.Application.Auth;

public class OAuthService(
    IAppUserRepository userRepo,
    IExternalLoginRepository externalLoginRepo,
    IGroupRepository groupRepo,
    AuthService authService,
    ILogger<OAuthService> logger)
{
    public async Task<AuthResponse> LoginOrRegisterFromOAuthAsync(
        string provider, string providerKey, string? email, string? name)
    {
        // Try to find existing external login
        var existingLogin = await externalLoginRepo.GetByProviderAsync(provider, providerKey);
        if (existingLogin is not null)
        {
            logger.LogInformation("OAuth login: existing {Provider} user {UserName}",
                provider, existingLogin.User.UserName);
            var token = authService.GenerateToken(existingLogin.User);
            return MapToResponse(existingLogin.User, token);
        }

        // Try to find user by email and link
        AppUser user;
        if (email is not null)
        {
            var existingUser = await userRepo.GetByEmailAsync(email);
            if (existingUser is not null)
            {
                user = existingUser;
                logger.LogInformation("OAuth: linking {Provider} to existing user {UserName}", provider, user.UserName);
            }
            else
            {
                user = await CreateNewOAuthUser(provider, providerKey, email, name);
            }
        }
        else
        {
            user = await CreateNewOAuthUser(provider, providerKey, email, name);
        }

        // Create external login link
        await externalLoginRepo.AddAsync(new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = provider,
            ProviderKey = providerKey,
            CreatedAt = DateTime.UtcNow
        });

        var jwtToken = authService.GenerateToken(user);
        return MapToResponse(user, jwtToken);
    }

    private async Task<AppUser> CreateNewOAuthUser(string provider, string providerKey, string? email, string? name)
    {
        var viewerGroup = await groupRepo.GetByNameAsync("Viewer")
            ?? throw new InvalidOperationException("Viewer group not found.");

        var userName = name ?? email?.Split('@')[0] ?? $"user_{Guid.NewGuid():N}"[..12];
        // Ensure unique username
        var baseName = userName;
        var counter = 1;
        while (await userRepo.GetByUserNameAsync(userName) is not null)
            userName = $"{baseName}{counter++}";

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            PasswordHash = null, // OAuth-only user
            IsRoot = false,
            CreatedAt = DateTime.UtcNow
        };
        user.Groups.Add(viewerGroup);

        await userRepo.AddAsync(user);
        logger.LogInformation("OAuth: created new user {UserName} via {Provider}", user.UserName, provider);

        return user;
    }

    private static AuthResponse MapToResponse(AppUser user, string token) =>
        new(user.Id, user.UserName, user.Email, user.IsRoot,
            user.Groups.Select(g => g.Name).ToList(), token);
}