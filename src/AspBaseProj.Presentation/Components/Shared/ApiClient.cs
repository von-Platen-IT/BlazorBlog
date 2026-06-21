using System.Net.Http.Json;
using System.Text.Json;
using AspBaseProj.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace AspBaseProj.Presentation.Components.Shared;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    // Auth
    public async Task<AuthUserInfo?> GetCurrentUserAsync()
    {
        // Read directly from the current HTTP context (Blazor Server)
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx?.User.Identity?.IsAuthenticated != true) return null;

        return new AuthUserInfo(
            ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "",
            ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "",
            ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            ctx.User.FindFirst("is_root")?.Value == "true",
            ctx.User.FindAll("groups").Select(c => c.Value).ToList()
        );
    }

    public async Task<AuthResponse?> LoginAsync(string userName, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", new { userName, password }, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
    }

    public async Task<AuthResponse?> RegisterAsync(string userName, string? email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register", new { userName, email, password }, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
    }

    public async Task LogoutAsync()
    {
        await _http.PostAsync("/api/auth/logout", null);
    }

    // Posts
    public async Task<PostListResponse?> GetPostsAsync(int page = 1, int pageSize = 10)
    {
        var response = await _http.GetAsync($"/api/posts?page={page}&pageSize={pageSize}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PostListResponse>(_jsonOptions);
    }

    public async Task<PostDto?> GetPostAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/posts/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PostDto>(_jsonOptions);
    }

    public async Task<PostDto?> CreatePostAsync(PostDto post)
    {
        var response = await _http.PostAsJsonAsync("/api/posts", post, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PostDto>(_jsonOptions);
    }

    public async Task<PostDto?> UpdatePostAsync(Guid id, PostDto post)
    {
        var response = await _http.PutAsJsonAsync($"/api/posts/{id}", post, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PostDto>(_jsonOptions);
    }

    public async Task<bool> DeletePostAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/posts/{id}");
        return response.IsSuccessStatusCode;
    }

    // Comments
    public async Task<List<CommentDto>?> GetCommentsAsync(Guid postId)
    {
        var response = await _http.GetAsync($"/api/comments/post/{postId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<CommentDto>>(_jsonOptions);
    }

    public async Task<CommentDto?> AddCommentAsync(Guid postId, string content, string? guestName = null, string? guestEmail = null)
    {
        var response = await _http.PostAsJsonAsync($"/api/comments/post/{postId}",
            new { content, guestName, guestEmail }, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CommentDto>(_jsonOptions);
    }

    public async Task<CommentDto?> ReplyToCommentAsync(Guid commentId, string content, string? guestName = null, string? guestEmail = null)
    {
        var response = await _http.PostAsJsonAsync($"/api/comments/{commentId}/reply",
            new { content, guestName, guestEmail }, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CommentDto>(_jsonOptions);
    }

    // Moderation
    public async Task<List<CommentDto>?> GetPendingCommentsAsync()
    {
        var response = await _http.GetAsync("/api/admin/comments/pending");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<CommentDto>>(_jsonOptions);
    }

    public async Task<bool> ApproveCommentAsync(Guid id)
    {
        var response = await _http.PostAsync($"/api/admin/comments/{id}/approve", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RejectCommentAsync(Guid id)
    {
        var response = await _http.PostAsync($"/api/admin/comments/{id}/reject", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BulkApproveAsync(List<Guid> ids)
    {
        var response = await _http.PostAsJsonAsync("/api/admin/comments/bulk-approve", ids, _jsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BulkRejectAsync(List<Guid> ids)
    {
        var response = await _http.PostAsJsonAsync("/api/admin/comments/bulk-reject", ids, _jsonOptions);
        return response.IsSuccessStatusCode;
    }

    // Users
    public async Task<List<UserDto>?> GetUsersAsync()
    {
        var response = await _http.GetAsync("/api/admin/users");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<UserDto>>(_jsonOptions);
    }

    public async Task<List<GroupDto>?> GetGroupsAsync()
    {
        var response = await _http.GetAsync("/api/admin/groups");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<GroupDto>>(_jsonOptions);
    }

    public async Task<bool> AssignGroupAsync(Guid userId, Guid groupId)
    {
        var response = await _http.PostAsync($"/api/admin/users/{userId}/groups?groupId={groupId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveGroupAsync(Guid userId, Guid groupId)
    {
        var response = await _http.DeleteAsync($"/api/admin/users/{userId}/groups/{groupId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var response = await _http.DeleteAsync($"/api/admin/users/{userId}");
        return response.IsSuccessStatusCode;
    }

    // Settings
    public async Task<List<SettingDto>?> GetSettingsAsync()
    {
        var response = await _http.GetAsync("/api/admin/settings");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<SettingDto>>(_jsonOptions);
    }

    public async Task<bool> UpdateSettingAsync(string key, string value)
    {
        var response = await _http.PutAsJsonAsync($"/api/admin/settings/{key}", new { value }, _jsonOptions);
        return response.IsSuccessStatusCode;
    }
}

// DTOs
public record AuthUserInfo(string UserId, string UserName, string? Email, bool IsRoot, List<string> Groups);
public record PostDto(Guid Id, string Title, string Content, Guid AuthorId, bool IsPublished, DateTime CreatedAt, DateTime? PublishedAt, AuthorDto? Author);
public record AuthorDto(Guid Id, string UserName);
public record PostListResponse(List<PostDto> Posts, int Total, int Page, int PageSize);
public record CommentDto(Guid Id, string Content, Guid PostId, Guid? UserId, Guid? ParentCommentId, string? GuestName, bool IsApproved, DateTime CreatedAt, UserRefDto? User, List<CommentDto> Replies);
public record UserRefDto(Guid Id, string UserName);
public record UserDto(Guid Id, string UserName, string? Email, bool IsRoot, DateTime CreatedAt, List<string> Groups);
public record GroupDto(Guid Id, string Name, string? Description);
public record SettingDto(string Key, string? Value, DateTime? UpdatedAt);