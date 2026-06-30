using System.Security.Claims;
using AspBaseProj.Application.Contracts.Auth;
using AspBaseProj.Application.Contracts.Comments;
using AspBaseProj.Application.Contracts.Posts;
using AspBaseProj.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AspBaseProj.Presentation.Controllers;

/// <summary>
/// Static helper methods shared across controllers.
/// </summary>
internal static class AuthHelper
{
    public static string? ResolveOAuthScheme(string provider) => provider.ToLower() switch
    {
        "google" => "Google",
        "github" => "GitHub",
        "microsoft" => "Microsoft",
        _ => null
    };

    public static async Task SignInWithCookie(HttpContext ctx, AuthResponse response)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, response.UserId.ToString()),
            new(ClaimTypes.Name, response.UserName),
            new("is_root", response.IsRoot.ToString().ToLower())
        };
        if (response.Email is not null) claims.Add(new(ClaimTypes.Email, response.Email));
        claims.AddRange(response.Groups.Select(g => new Claim("groups", g)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    public static PostDto MapPost(Post p) => new PostDto(
        p.Id, p.Title, p.Content, p.AuthorId, p.IsPublished, p.CreatedAt, p.PublishedAt,
        p.Author is not null ? new AuthorDto(p.Author.Id, p.Author.UserName) : null,
        0, 0
    );

    public static CommentDto MapComment(Comment c) => new CommentDto(
        c.Id, c.Content, c.PostId, c.UserId, c.ParentCommentId, c.GuestName, c.IsApproved, c.CreatedAt,
        c.User is not null ? new UserRefDto(c.User.Id, c.User.UserName) : null,
        c.Replies.Select(MapComment).ToList(),
        c.Post?.Title
    );
}