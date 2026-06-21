using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AspBaseProj.Application.Common;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
    private readonly ClaimsPrincipal? _user = httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = _user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return value is not null && Guid.TryParse(value, out var guid) ? guid : null;
        }
    }

    public string? UserName =>
        _user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

    public bool IsRoot =>
        _user?.Claims.FirstOrDefault(c => c.Type == "is_root")?.Value
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    public List<string> Groups =>
        _user?.Claims.Where(c => c.Type == "groups").Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated == true;

    public bool IsInGroup(string groupName) =>
        Groups.Contains(groupName, StringComparer.OrdinalIgnoreCase);
}