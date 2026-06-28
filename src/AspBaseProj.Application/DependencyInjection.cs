using AspBaseProj.Application.Auth;
using AspBaseProj.Application.Common;
using AspBaseProj.Application.Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace AspBaseProj.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<OAuthService>();
        services.AddScoped<CurrentUserService>();
        services.AddHttpContextAccessor();

        return services;
    }
}