using AspBaseProj.Domain.Interfaces;
using AspBaseProj.Infrastructure.Data;
using AspBaseProj.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspBaseProj.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<BlogDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IPostRatingRepository, PostRatingRepository>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();

        return services;
    }
}