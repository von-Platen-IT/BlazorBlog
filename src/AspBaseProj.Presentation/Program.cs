using System.Security.Claims;
using System.Text;
using AspBaseProj.Application;
using AspBaseProj.Application.Auth;
using AspBaseProj.Application.Common;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using AspBaseProj.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Application
builder.Services.AddApplication();

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBootstrapBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AspBaseProj.Presentation.Components.Shared.CookieForwardingHandler>();
builder.Services.AddScoped<HttpClient>(sp =>
{
    var handler = sp.GetRequiredService<AspBaseProj.Presentation.Components.Shared.CookieForwardingHandler>();
    handler.InnerHandler = new HttpClientHandler();
    var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
    return client;
});
builder.Services.AddScoped<AspBaseProj.Presentation.Components.Shared.ApiClient>();

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = "groups",
        NameClaimType = ClaimTypes.Name
    };
});

// OAuth Providers (only if configured)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
    builder.Services.AddAuthentication().AddGoogle(o =>
    {
        o.ClientId = googleClientId;
        o.ClientSecret = googleClientSecret;
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
if (!string.IsNullOrEmpty(githubClientId) && !string.IsNullOrEmpty(githubClientSecret))
    builder.Services.AddAuthentication().AddGitHub(o =>
    {
        o.ClientId = githubClientId;
        o.ClientSecret = githubClientSecret;
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

var msClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var msClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
    builder.Services.AddAuthentication().AddMicrosoftAccount(o =>
    {
        o.ClientId = msClientId;
        o.ClientSecret = msClientSecret;
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

// Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RootPolicy", p => p.RequireClaim("is_root", "true"))
    .AddPolicy("AdminPolicy", p => p.RequireClaim("groups", "Admin"))
    .AddPolicy("AuthorPolicy", p => p.RequireClaim("groups", "Author"))
    .AddPolicy("AdminOrRootPolicy", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("is_root", "true") ||
            ctx.User.HasClaim("groups", "Admin")))
    .AddPolicy("AuthorOrAdminOrRootPolicy", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("is_root", "true") ||
            ctx.User.HasClaim("groups", "Admin") ||
            ctx.User.HasClaim("groups", "Author")));

var app = builder.Build();

// Auto-migrate and seed
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DatabaseSeeder.SeedAsync(app.Services, builder.Configuration, logger);
}

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseAntiforgery();

// Swagger
app.MapOpenApi();

// Blazor
app.MapRazorComponents<AspBaseProj.Presentation.Components.App>()
    .AddInteractiveServerRenderMode();

// ============================================================
// AUTH ENDPOINTS
// ============================================================
var authGroup = app.MapGroup("/api/auth");

authGroup.MapPost("/register", async (HttpContext ctx, AuthService authService) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var userName = form["userName"].ToString();
    var email = form["email"].ToString();
    var password = form["password"].ToString();

    try
    {
        var response = await authService.RegisterAsync(new RegisterRequest(userName, string.IsNullOrEmpty(email) ? null : email, password));
        await SignInWithCookie(ctx, response);
        ctx.Response.Redirect("/");
    }
    catch (InvalidOperationException ex)
    {
        ctx.Response.Redirect($"/register?error={Uri.EscapeDataString(ex.Message)}");
    }
}).DisableAntiforgery();

authGroup.MapPost("/login", async (HttpContext ctx, AuthService authService) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var userName = form["userName"].ToString();
    var password = form["password"].ToString();

    try
    {
        var response = await authService.LoginAsync(new LoginRequest(userName, password));
        await SignInWithCookie(ctx, response);
        ctx.Response.Redirect("/");
    }
    catch (UnauthorizedAccessException)
    {
        ctx.Response.Redirect("/login?error=Invalid%20credentials");
    }
    catch (InvalidOperationException)
    {
        ctx.Response.Redirect("/login?error=Login%20failed");
    }
}).DisableAntiforgery();

authGroup.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/");
}).DisableAntiforgery();

authGroup.MapGet("/me", (HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    return Results.Ok(new
    {
        UserId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier),
        UserName = ctx.User.FindFirstValue(ClaimTypes.Name),
        Email = ctx.User.FindFirstValue(ClaimTypes.Email),
        IsRoot = ctx.User.FindFirstValue("is_root") == "true",
        Groups = ctx.User.FindAll("groups").Select(c => c.Value).ToList()
    });
}).RequireAuthorization();

authGroup.MapGet("/login/{provider}", (string provider) =>
{
    var scheme = ResolveOAuthScheme(provider);
    if (scheme is null) return Results.BadRequest(new { error = $"Unknown provider: {provider}" });
    return Results.Challenge(new AuthenticationProperties { RedirectUri = $"/api/auth/callback/{provider}" }, [scheme]);
});

authGroup.MapGet("/callback/{provider}", async (string provider, HttpContext ctx, OAuthService oauthService) =>
{
    var scheme = ResolveOAuthScheme(provider);
    if (scheme is null) return Results.BadRequest(new { error = $"Unknown provider: {provider}" });

    var authResult = await ctx.AuthenticateAsync(scheme);
    if (!authResult.Succeeded) return Results.Unauthorized();

    var claims = authResult.Principal;
    var providerKey = claims.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var email = claims.FindFirstValue(ClaimTypes.Email);
    var name = claims.FindFirstValue(ClaimTypes.Name);

    var response = await oauthService.LoginOrRegisterFromOAuthAsync(provider, providerKey, email, name);
    await SignInWithCookie(ctx, response);
    return Results.Redirect("/");
});

// ============================================================
// POSTS ENDPOINTS
// ============================================================
var postsGroup = app.MapGroup("/api/posts");

postsGroup.MapGet("/", async (IPostRepository repo, int page = 1, int pageSize = 10) =>
{
    var (posts, total) = await repo.GetPublishedAsync(page, pageSize);
    return Results.Ok(new { posts = posts.Select(MapPost), total, page, pageSize });
});

postsGroup.MapGet("/{id:guid}", async (Guid id, IPostRepository repo) =>
{
    var post = await repo.GetByIdAsync(id);
    return post is not null && post.IsPublished ? Results.Ok(MapPost(post)) : Results.NotFound();
});

postsGroup.MapPost("/", async (Post post, IPostRepository repo, CurrentUserService user) =>
{
    if (!user.IsAuthenticated) return Results.Unauthorized();
    post.Id = Guid.NewGuid();
    post.AuthorId = user.UserId!.Value;
    post.CreatedAt = DateTime.UtcNow;
    if (post.IsPublished) post.PublishedAt = DateTime.UtcNow;
    var created = await repo.AddAsync(post);
    return Results.Created($"/api/posts/{created.Id}", MapPost(created));
}).RequireAuthorization("AuthorOrAdminOrRootPolicy");

postsGroup.MapPut("/{id:guid}", async (Guid id, Post input, IPostRepository repo, CurrentUserService user) =>
{
    var post = await repo.GetByIdAsync(id);
    if (post is null) return Results.NotFound();
    if (!user.IsRoot && !user.IsInGroup("Admin") && post.AuthorId != user.UserId)
        return Results.Forbid();

    post.Title = input.Title;
    post.Content = input.Content;
    post.IsPublished = input.IsPublished;
    post.UpdatedAt = DateTime.UtcNow;
    if (input.IsPublished && post.PublishedAt is null) post.PublishedAt = DateTime.UtcNow;
    await repo.UpdateAsync(post);
    return Results.Ok(MapPost(post));
}).RequireAuthorization("AuthorOrAdminOrRootPolicy");

postsGroup.MapDelete("/{id:guid}", async (Guid id, IPostRepository repo, CurrentUserService user) =>
{
    var post = await repo.GetByIdAsync(id);
    if (post is null) return Results.NotFound();
    if (!user.IsRoot && !user.IsInGroup("Admin") && post.AuthorId != user.UserId)
        return Results.Forbid();

    await repo.DeleteAsync(post);
    return Results.NoContent();
}).RequireAuthorization("AuthorOrAdminOrRootPolicy");

// ============================================================
// MEDIA ENDPOINTS
// ============================================================
var mediaGroup = app.MapGroup("/api/media");

mediaGroup.MapGet("/post/{postId:guid}", async (Guid postId, IMediaRepository repo) =>
{
    var media = await repo.GetByPostIdAsync(postId);
    return Results.Ok(media.Select(m => new { m.Id, m.FileName, m.ContentType, m.CreatedAt }));
});

mediaGroup.MapPost("/post/{postId:guid}", async (Guid postId, HttpRequest request, IMediaRepository repo, IPostRepository postRepo, CurrentUserService user) =>
{
    var post = await postRepo.GetByIdAsync(postId);
    if (post is null) return Results.NotFound();
    if (!user.IsRoot && !user.IsInGroup("Admin") && post.AuthorId != user.UserId)
        return Results.Forbid();

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null) return Results.BadRequest(new { error = "No file provided." });

    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
    if (!allowedTypes.Contains(file.ContentType))
        return Results.BadRequest(new { error = "Invalid file type." });
    if (file.Length > 5 * 1024 * 1024)
        return Results.BadRequest(new { error = "File too large. Max 5 MB." });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var mediaItem = new Media
    {
        Id = Guid.NewGuid(),
        PostId = postId,
        FileName = file.FileName,
        ContentType = file.ContentType,
        Data = ms.ToArray(),
        CreatedAt = DateTime.UtcNow
    };
    await repo.AddAsync(mediaItem);
    return Results.Created($"/api/media/{mediaItem.Id}", new { mediaItem.Id, mediaItem.FileName, mediaItem.ContentType });
}).RequireAuthorization("AuthorOrAdminOrRootPolicy");

mediaGroup.MapGet("/{id:guid}/data", async (Guid id, IMediaRepository repo) =>
{
    var media = await repo.GetByIdAsync(id);
    return media is not null ? Results.File(media.Data, media.ContentType, media.FileName) : Results.NotFound();
});

mediaGroup.MapDelete("/{id:guid}", async (Guid id, IMediaRepository repo, IPostRepository postRepo, CurrentUserService user) =>
{
    var media = await repo.GetByIdAsync(id);
    if (media is null) return Results.NotFound();
    var post = await postRepo.GetByIdAsync(media.PostId);
    if (post is null) return Results.NotFound();
    if (!user.IsRoot && !user.IsInGroup("Admin") && post.AuthorId != user.UserId)
        return Results.Forbid();

    await repo.DeleteAsync(media);
    return Results.NoContent();
}).RequireAuthorization("AuthorOrAdminOrRootPolicy");

// ============================================================
// COMMENTS ENDPOINTS
// ============================================================
var commentsGroup = app.MapGroup("/api/comments");

commentsGroup.MapGet("/post/{postId:guid}", async (Guid postId, ICommentRepository repo) =>
{
    var comments = await repo.GetByPostIdAsync(postId);
    return Results.Ok(comments.Select(MapComment));
});

commentsGroup.MapPost("/post/{postId:guid}", async (Guid postId, Comment comment, ICommentRepository repo, CurrentUserService user) =>
{
    comment.Id = Guid.NewGuid();
    comment.PostId = postId;
    comment.CreatedAt = DateTime.UtcNow;

    if (user.IsAuthenticated)
    {
        comment.UserId = user.UserId;
        comment.IsApproved = true;
    }
    else
    {
        comment.IsApproved = false;
        if (string.IsNullOrWhiteSpace(comment.GuestName) || string.IsNullOrWhiteSpace(comment.GuestEmail))
            return Results.BadRequest(new { error = "Guest name and email are required." });
    }

    var created = await repo.AddAsync(comment);
    return Results.Created($"/api/comments/{created.Id}", MapComment(created));
});

commentsGroup.MapPost("/{id:guid}/reply", async (Guid id, Comment reply, ICommentRepository repo, CurrentUserService user) =>
{
    var parent = await repo.GetByIdAsync(id);
    if (parent is null) return Results.NotFound();

    reply.Id = Guid.NewGuid();
    reply.PostId = parent.PostId;
    reply.ParentCommentId = id;
    reply.CreatedAt = DateTime.UtcNow;

    if (user.IsAuthenticated)
    {
        reply.UserId = user.UserId;
        reply.IsApproved = true;
    }
    else
    {
        reply.IsApproved = false;
        if (string.IsNullOrWhiteSpace(reply.GuestName) || string.IsNullOrWhiteSpace(reply.GuestEmail))
            return Results.BadRequest(new { error = "Guest name and email are required." });
    }

    var created = await repo.AddAsync(reply);
    return Results.Created($"/api/comments/{created.Id}", MapComment(created));
});

// ============================================================
// MODERATION ENDPOINTS (Admin/Root)
// ============================================================
var modGroup = app.MapGroup("/api/admin/comments").RequireAuthorization("AdminOrRootPolicy");

modGroup.MapGet("/pending", async (ICommentRepository repo) =>
{
    var pending = await repo.GetPendingAsync();
    return Results.Ok(pending.Select(MapComment));
});

modGroup.MapPost("/{id:guid}/approve", async (Guid id, ICommentRepository repo) =>
{
    var comment = await repo.GetByIdAsync(id);
    if (comment is null) return Results.NotFound();
    comment.IsApproved = true;
    await repo.UpdateAsync(comment);
    return Results.Ok(MapComment(comment));
});

modGroup.MapPost("/{id:guid}/reject", async (Guid id, ICommentRepository repo) =>
{
    var comment = await repo.GetByIdAsync(id);
    if (comment is null) return Results.NotFound();
    await repo.DeleteAsync(comment);
    return Results.NoContent();
});

modGroup.MapPost("/bulk-approve", async (Guid[] ids, ICommentRepository repo) =>
{
    foreach (var id in ids)
    {
        var comment = await repo.GetByIdAsync(id);
        if (comment is not null) { comment.IsApproved = true; await repo.UpdateAsync(comment); }
    }
    return Results.Ok(new { approved = ids.Length });
});

modGroup.MapPost("/bulk-reject", async (Guid[] ids, ICommentRepository repo) =>
{
    foreach (var id in ids)
    {
        var comment = await repo.GetByIdAsync(id);
        if (comment is not null) await repo.DeleteAsync(comment);
    }
    return Results.Ok(new { rejected = ids.Length });
});

// ============================================================
// USER MANAGEMENT ENDPOINTS (Root only)
// ============================================================
var adminGroup = app.MapGroup("/api/admin").RequireAuthorization("RootPolicy");

adminGroup.MapGet("/groups", async (IGroupRepository repo) =>
{
    var groups = await repo.GetAllAsync();
    return Results.Ok(groups.Select(g => new { g.Id, g.Name, g.Description }));
});

var usersGroup = app.MapGroup("/api/admin/users").RequireAuthorization("RootPolicy");

usersGroup.MapGet("/", async (IAppUserRepository repo) =>
{
    var users = await repo.GetAllAsync();
    return Results.Ok(users.Select(u => new
    {
        u.Id, u.UserName, u.Email, u.IsRoot, u.CreatedAt,
        Groups = u.Groups.Select(g => g.Name).ToList()
    }));
});

usersGroup.MapGet("/{id:guid}", async (Guid id, IAppUserRepository repo) =>
{
    var user = await repo.GetByIdAsync(id);
    return user is not null ? Results.Ok(new
    {
        user.Id, user.UserName, user.Email, user.IsRoot, user.CreatedAt,
        Groups = user.Groups.Select(g => g.Name).ToList()
    }) : Results.NotFound();
});

usersGroup.MapPost("/{id:guid}/groups", async (Guid id, Guid groupId, IAppUserRepository userRepo, IGroupRepository groupRepo) =>
{
    var user = await userRepo.GetByIdAsync(id);
    if (user is null) return Results.NotFound();
    if (user.IsRoot) return Results.BadRequest(new { error = "Cannot modify root user." });

    var group = await groupRepo.GetByIdAsync(groupId);
    if (group is null) return Results.NotFound();

    if (!user.Groups.Any(g => g.Id == groupId))
    {
        user.Groups.Add(group);
        await userRepo.UpdateAsync(user);
    }
    return Results.Ok(new { user.Id, Groups = user.Groups.Select(g => g.Name).ToList() });
});

usersGroup.MapDelete("/{id:guid}/groups/{groupId:guid}", async (Guid id, Guid groupId, IAppUserRepository userRepo) =>
{
    var user = await userRepo.GetByIdAsync(id);
    if (user is null) return Results.NotFound();
    if (user.IsRoot) return Results.BadRequest(new { error = "Cannot modify root user." });

    var group = user.Groups.FirstOrDefault(g => g.Id == groupId);
    if (group is not null)
    {
        user.Groups.Remove(group);
        await userRepo.UpdateAsync(user);
    }
    return Results.Ok(new { user.Id, Groups = user.Groups.Select(g => g.Name).ToList() });
});

usersGroup.MapDelete("/{id:guid}", async (Guid id, IAppUserRepository repo) =>
{
    var user = await repo.GetByIdAsync(id);
    if (user is null) return Results.NotFound();
    if (user.IsRoot) return Results.BadRequest(new { error = "Cannot delete root user." });
    await repo.DeleteAsync(user);
    return Results.NoContent();
});

// ============================================================
// SYSTEM SETTINGS ENDPOINTS (Root only)
// ============================================================
var settingsGroup = app.MapGroup("/api/admin/settings").RequireAuthorization("RootPolicy");

settingsGroup.MapGet("/", async (ISystemSettingRepository repo) =>
{
    var settings = await repo.GetAllAsync();
    return Results.Ok(settings.Select(s => new { s.Key, s.Value, s.UpdatedAt }));
});

settingsGroup.MapPut("/{key}", async (string key, UpdateSettingRequest request, ISystemSettingRepository repo) =>
{
    await repo.SetAsync(new SystemSetting { Key = key, Value = request.Value });
    return Results.Ok(new { key, request.Value });
});

app.Run();

// ============================================================
// HELPERS
// ============================================================
static string? ResolveOAuthScheme(string provider) => provider.ToLower() switch
{
    "google" => "Google",
    "github" => "GitHub",
    "microsoft" => "Microsoft",
    _ => null
};

static async Task SignInWithCookie(HttpContext ctx, AuthResponse response)
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

static object MapPost(Post p) => new
{
    p.Id, p.Title, p.Content, p.AuthorId, p.IsPublished, p.CreatedAt, p.UpdatedAt, p.PublishedAt,
    Author = p.Author is not null ? new { p.Author.Id, p.Author.UserName } : null
};

static object MapComment(Comment c) => new
{
    c.Id, c.Content, c.PostId, c.UserId, c.ParentCommentId, c.GuestName, c.IsApproved, c.CreatedAt,
    User = c.User is not null ? new { c.User.Id, c.User.UserName } : null,
    Replies = c.Replies.Select(MapComment)
};

// ============================================================
// DTOs
// ============================================================
public record UpdateSettingRequest(string Value);
