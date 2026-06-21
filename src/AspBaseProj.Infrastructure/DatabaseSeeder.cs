using AspBaseProj.Domain.Entities;
using AspBaseProj.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspBaseProj.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.Groups.AnyAsync())
        {
            db.Groups.AddRange(
                new Group { Id = Guid.NewGuid(), Name = "Author", Description = "Can create, edit, and delete own blog posts" },
                new Group { Id = Guid.NewGuid(), Name = "Admin", Description = "Can manage all posts and moderate comments" },
                new Group { Id = Guid.NewGuid(), Name = "Viewer", Description = "Can read posts and comment" },
                new Group { Id = Guid.NewGuid(), Name = "Public", Description = "Can read published posts only" }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded groups: Author, Admin, Viewer, Public");
        }

        if (!await db.AppUsers.AnyAsync(u => u.IsRoot))
        {
            var rootUsername = configuration["Blog:RootUsername"] ?? "root";
            var rootPassword = configuration["Blog:RootPassword"] ?? "Root#12345!";
            var rootUser = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = rootUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(rootPassword),
                IsRoot = true,
                CreatedAt = DateTime.UtcNow
            };
            db.AppUsers.Add(rootUser);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded root user: {Username}", rootUsername);
        }

        if (!await db.SystemSettings.AnyAsync())
        {
            db.SystemSettings.AddRange(
                new SystemSetting { Id = Guid.NewGuid(), Key = "BlogTitle", Value = "My Blog", UpdatedAt = DateTime.UtcNow },
                new SystemSetting { Id = Guid.NewGuid(), Key = "ModerationEnabled", Value = "true", UpdatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default system settings");
        }
    }
}