using AspBaseProj.Application.Auth;
using AspBaseProj.Application.Common;
using AspBaseProj.Application.Contracts.Auth;
using AspBaseProj.Application.Contracts.Bookmarks;
using AspBaseProj.Application.Contracts.Comments;
using AspBaseProj.Application.Contracts.Posts;
using AspBaseProj.Application.Contracts.Ratings;
using AspBaseProj.Application.Contracts.Settings;
using AspBaseProj.Application.Contracts.Users;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace AspBaseProj.Presentation.Components.Shared;

public class ApiClient
{
    private readonly CurrentUserService _currentUser;
    private readonly AuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPostRepository _postRepo;
    private readonly IPostRatingRepository _ratingRepo;
    private readonly ICommentRepository _commentRepo;
    private readonly IBookmarkRepository _bookmarkRepo;
    private readonly IAppUserRepository _userRepo;
    private readonly IGroupRepository _groupRepo;
    private readonly ISystemSettingRepository _settingRepo;
    private readonly IMediaRepository _mediaRepo;

    public ApiClient(
        CurrentUserService currentUser,
        AuthService authService,
        IHttpContextAccessor httpContextAccessor,
        IPostRepository postRepo,
        IPostRatingRepository ratingRepo,
        ICommentRepository commentRepo,
        IBookmarkRepository bookmarkRepo,
        IAppUserRepository userRepo,
        IGroupRepository groupRepo,
        ISystemSettingRepository settingRepo,
        IMediaRepository mediaRepo)
    {
        _currentUser = currentUser;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
        _postRepo = postRepo;
        _ratingRepo = ratingRepo;
        _commentRepo = commentRepo;
        _bookmarkRepo = bookmarkRepo;
        _userRepo = userRepo;
        _groupRepo = groupRepo;
        _settingRepo = settingRepo;
        _mediaRepo = mediaRepo;
    }

    // ────────────────────────────── Auth ──────────────────────────────

    public Task<AuthUserInfo?> GetCurrentUserAsync()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Task.FromResult<AuthUserInfo?>(null);

        return Task.FromResult<AuthUserInfo?>(new AuthUserInfo(
            _currentUser.UserId.Value.ToString(),
            _currentUser.UserName ?? "",
            null, // Email not exposed by CurrentUserService; available in HttpContext if needed later
            _currentUser.IsRoot,
            _currentUser.Groups
        ));
    }

    public async Task<AuthResponse?> LoginAsync(string userName, string password)
    {
        try
        {
            return await _authService.LoginAsync(new LoginRequest(userName, password));
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponse?> RegisterAsync(string userName, string? email, string password)
    {
        try
        {
            return await _authService.RegisterAsync(new RegisterRequest(userName, email, password));
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    // ────────────────────────────── Posts ─────────────────────────────

    public async Task<PostListResponse?> GetPostsAsync(int page = 1, int pageSize = 10)
    {
        var (posts, total) = await _postRepo.GetPublishedAsync(page, pageSize);
        var postIds = posts.Select(p => p.Id).ToList();
        var ratingCounts = await _ratingRepo.GetCountsByPostIdsAsync(postIds);
        var dtos = posts.Select(p =>
        {
            var (likeCount, dislikeCount) = ratingCounts.TryGetValue(p.Id, out var rc) ? rc : (0, 0);
            return new PostDto(
                p.Id, p.Title, p.Content, p.AuthorId, p.IsPublished, p.CreatedAt, p.PublishedAt,
                p.Author is not null ? new AuthorDto(p.Author.Id, p.Author.UserName) : null,
                likeCount, dislikeCount
            );
        }).ToList();
        return new PostListResponse(dtos, total, page, pageSize);
    }

    public async Task<PostDto?> GetPostAsync(Guid id)
    {
        var post = await _postRepo.GetByIdAsync(id);
        if (post is null) return null;

        if (post.IsPublished)
            return MapPost(post);

        if (_currentUser.IsAuthenticated && (post.AuthorId == _currentUser.UserId || _currentUser.IsRoot || _currentUser.IsInGroup("Admin")))
            return MapPost(post);

        return null;
    }

    public async Task<PostDto?> CreatePostAsync(PostDto post)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return null;

        var entity = new Post
        {
            Id = Guid.NewGuid(),
            Title = post.Title,
            Content = post.Content,
            AuthorId = _currentUser.UserId.Value,
            IsPublished = post.IsPublished,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = post.IsPublished ? DateTime.UtcNow : null
        };

        var created = await _postRepo.AddAsync(entity);
        return MapPost(created);
    }

    public async Task<PostDto?> UpdatePostAsync(Guid id, PostDto post)
    {
        var existing = await _postRepo.GetByIdAsync(id);
        if (existing is null) return null;

        if (!_currentUser.IsRoot && !_currentUser.IsInGroup("Admin") && existing.AuthorId != _currentUser.UserId)
            return null;

        existing.Title = post.Title;
        existing.Content = post.Content;
        existing.IsPublished = post.IsPublished;
        existing.UpdatedAt = DateTime.UtcNow;
        if (post.IsPublished && existing.PublishedAt is null)
            existing.PublishedAt = DateTime.UtcNow;

        await _postRepo.UpdateAsync(existing);
        return MapPost(existing);
    }

    public async Task<bool> DeletePostAsync(Guid id)
    {
        var post = await _postRepo.GetByIdAsync(id);
        if (post is null) return false;

        if (!_currentUser.IsRoot && !_currentUser.IsInGroup("Admin") && post.AuthorId != _currentUser.UserId)
            return false;

        await _postRepo.DeleteAsync(post);
        return true;
    }

    // ────────────────────────────── My Posts ──────────────────────────

    public async Task<MyPostsResponse?> GetMyPostsAsync(int page = 1, int pageSize = 10)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return null;

        var (posts, total) = await _postRepo.GetByAuthorIdPaginatedAsync(_currentUser.UserId.Value, page, pageSize);
        var postIds = posts.Select(p => p.Id).ToList();
        var commentCounts = await _commentRepo.GetCommentCountsByPostIdsAsync(postIds);

        var dtos = posts.Select(p => new MyPostDto(
            p.Id,
            p.Title,
            p.Content.Length > 200 ? p.Content[..200] + "..." : p.Content,
            p.IsPublished,
            p.CreatedAt,
            p.UpdatedAt,
            p.PublishedAt,
            commentCounts.TryGetValue(p.Id, out var count) ? count : 0
        )).ToList();

        return new MyPostsResponse(dtos, total, page, pageSize);
    }

    // ────────────────────────────── Comments ──────────────────────────

    public async Task<List<CommentDto>?> GetCommentsAsync(Guid postId)
    {
        var comments = await _commentRepo.GetByPostIdAsync(postId);
        return comments.Select(MapComment).ToList();
    }

    public async Task<CommentDto?> AddCommentAsync(Guid postId, string content, string? guestName = null, string? guestEmail = null)
    {
        var entity = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        if (_currentUser.IsAuthenticated)
        {
            entity.UserId = _currentUser.UserId;
            entity.IsApproved = true;
        }
        else
        {
            entity.IsApproved = false;
            entity.GuestName = guestName;
            entity.GuestEmail = guestEmail;
        }

        var created = await _commentRepo.AddAsync(entity);
        return MapComment(created);
    }

    public async Task<CommentDto?> ReplyToCommentAsync(Guid commentId, string content, string? guestName = null, string? guestEmail = null)
    {
        var parent = await _commentRepo.GetByIdAsync(commentId);
        if (parent is null) return null;

        var entity = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = parent.PostId,
            ParentCommentId = commentId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        if (_currentUser.IsAuthenticated)
        {
            entity.UserId = _currentUser.UserId;
            entity.IsApproved = true;
        }
        else
        {
            entity.IsApproved = false;
            entity.GuestName = guestName;
            entity.GuestEmail = guestEmail;
        }

        var created = await _commentRepo.AddAsync(entity);
        return MapComment(created);
    }

    // ────────────────────────────── Moderation ────────────────────────

    public async Task<List<CommentDto>?> GetPendingCommentsAsync()
    {
        var pending = await _commentRepo.GetPendingAsync();
        return pending.Select(MapComment).ToList();
    }

    public async Task<bool> ApproveCommentAsync(Guid id)
    {
        var comment = await _commentRepo.GetByIdAsync(id);
        if (comment is null) return false;

        comment.IsApproved = true;
        await _commentRepo.UpdateAsync(comment);
        return true;
    }

    public async Task<bool> RejectCommentAsync(Guid id)
    {
        var comment = await _commentRepo.GetByIdAsync(id);
        if (comment is null) return false;

        await _commentRepo.DeleteAsync(comment);
        return true;
    }

    public async Task<bool> BulkApproveAsync(List<Guid> ids)
    {
        foreach (var id in ids)
        {
            var comment = await _commentRepo.GetByIdAsync(id);
            if (comment is not null)
            {
                comment.IsApproved = true;
                await _commentRepo.UpdateAsync(comment);
            }
        }
        return true;
    }

    public async Task<bool> BulkRejectAsync(List<Guid> ids)
    {
        foreach (var id in ids)
        {
            var comment = await _commentRepo.GetByIdAsync(id);
            if (comment is not null)
                await _commentRepo.DeleteAsync(comment);
        }
        return true;
    }

    // ────────────────────────────── Users ─────────────────────────────

    public async Task<List<UserDto>?> GetUsersAsync()
    {
        var users = await _userRepo.GetAllAsync();
        return users.Select(u => new UserDto(
            u.Id, u.UserName, u.Email, u.IsRoot, u.CreatedAt,
            u.Groups.Select(g => g.Name).ToList()
        )).ToList();
    }

    public async Task<List<GroupDto>?> GetGroupsAsync()
    {
        var groups = await _groupRepo.GetAllAsync();
        return groups.Select(g => new GroupDto(g.Id, g.Name, g.Description)).ToList();
    }

    public async Task<bool> AssignGroupAsync(Guid userId, Guid groupId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null || user.IsRoot) return false;

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group is null) return false;

        if (!user.Groups.Any(g => g.Id == groupId))
        {
            user.Groups.Add(group);
            await _userRepo.UpdateAsync(user);
        }
        return true;
    }

    public async Task<bool> RemoveGroupAsync(Guid userId, Guid groupId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null || user.IsRoot) return false;

        var group = user.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is not null)
        {
            user.Groups.Remove(group);
            await _userRepo.UpdateAsync(user);
        }
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null || user.IsRoot) return false;

        await _userRepo.DeleteAsync(user);
        return true;
    }

    // ────────────────────────────── Settings ──────────────────────────

    public async Task<List<SettingDto>?> GetSettingsAsync()
    {
        var settings = await _settingRepo.GetAllAsync();
        return settings.Select(s => new SettingDto(s.Key, s.Value, s.UpdatedAt)).ToList();
    }

    public async Task<bool> UpdateSettingAsync(string key, string value)
    {
        await _settingRepo.SetAsync(new SystemSetting { Key = key, Value = value });
        return true;
    }

    // ────────────────────────────── Ratings ───────────────────────────

    public async Task<RatingResponse?> LikePostAsync(Guid postId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return null;

        var post = await _postRepo.GetByIdAsync(postId);
        if (post is null) return null;

        var existing = await _ratingRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
        if (existing is not null && existing.IsLike)
        {
            await _ratingRepo.DeleteAsync(existing);
        }
        else if (existing is not null)
        {
            existing.IsLike = true;
            await _ratingRepo.UpdateAsync(existing);
        }
        else
        {
            await _ratingRepo.AddAsync(new PostRating
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = _currentUser.UserId.Value,
                IsLike = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var (likeCount, dislikeCount) = await _ratingRepo.GetCountsAsync(postId);
        var userRating = await _ratingRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
        return new RatingResponse(likeCount, dislikeCount, userRating is not null ? (userRating.IsLike ? "like" : "dislike") : null);
    }

    public async Task<RatingResponse?> DislikePostAsync(Guid postId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return null;

        var post = await _postRepo.GetByIdAsync(postId);
        if (post is null) return null;

        var existing = await _ratingRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
        if (existing is not null && !existing.IsLike)
        {
            await _ratingRepo.DeleteAsync(existing);
        }
        else if (existing is not null)
        {
            existing.IsLike = false;
            await _ratingRepo.UpdateAsync(existing);
        }
        else
        {
            await _ratingRepo.AddAsync(new PostRating
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = _currentUser.UserId.Value,
                IsLike = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var (likeCount, dislikeCount) = await _ratingRepo.GetCountsAsync(postId);
        var userRating = await _ratingRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
        return new RatingResponse(likeCount, dislikeCount, userRating is not null ? (userRating.IsLike ? "like" : "dislike") : null);
    }

    public async Task<RatingResponse?> GetPostRatingAsync(Guid postId)
    {
        var (likeCount, dislikeCount) = await _ratingRepo.GetCountsAsync(postId);
        string? userRating = null;
        if (_currentUser.IsAuthenticated && _currentUser.UserId is not null)
        {
            var existing = await _ratingRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
            userRating = existing is not null ? (existing.IsLike ? "like" : "dislike") : null;
        }
        return new RatingResponse(likeCount, dislikeCount, userRating);
    }

    // ────────────────────────────── Bookmarks ─────────────────────────

    public async Task<BookmarkToggleResponse?> ToggleBookmarkAsync(Guid postId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return null;

        var post = await _postRepo.GetByIdAsync(postId);
        if (post is null) return null;

        var existing = await _bookmarkRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
        if (existing is not null)
        {
            await _bookmarkRepo.DeleteAsync(existing);
            return new BookmarkToggleResponse(false);
        }

        await _bookmarkRepo.AddAsync(new Bookmark
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = _currentUser.UserId.Value,
            CreatedAt = DateTime.UtcNow
        });
        return new BookmarkToggleResponse(true);
    }

    public async Task<BookmarkStatusResponse?> GetBookmarkStatusAsync(Guid postId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return new BookmarkStatusResponse(false);

        var existing = await _bookmarkRepo.GetByPostAndUserAsync(postId, _currentUser.UserId.Value);
        return new BookmarkStatusResponse(existing is not null);
    }

    public async Task<BookmarkedPostsResponse?> GetBookmarkedPostsAsync(int page = 1, int pageSize = 10)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return null;

        var (posts, total) = await _bookmarkRepo.GetBookmarkedPostsAsync(_currentUser.UserId.Value, page, pageSize);
        var postIds = posts.Select(p => p.Id).ToList();
        var ratingCounts = await _ratingRepo.GetCountsByPostIdsAsync(postIds);
        var commentCounts = await _commentRepo.GetCommentCountsByPostIdsAsync(postIds);

        var dtos = posts.Select(p =>
        {
            var rc = ratingCounts.TryGetValue(p.Id, out var r) ? r : (LikeCount: 0, DislikeCount: 0);
            return new BookmarkedPostDto(
                p.Id,
                p.Title,
                p.Content.Length > 200 ? p.Content[..200] + "..." : p.Content,
                p.Author.UserName,
                p.IsPublished,
                p.PublishedAt,
                rc.LikeCount,
                rc.DislikeCount,
                commentCounts.TryGetValue(p.Id, out var cc) ? cc : 0
            );
        }).ToList();

        return new BookmarkedPostsResponse(dtos, total, page, pageSize);
    }

    // ────────────────────────────── Media ─────────────────────────────

    public async Task<List<MediaInfo>> GetMediaAsync(Guid postId)
    {
        var media = await _mediaRepo.GetByPostIdAsync(postId);
        return media.Select(m => new MediaInfo(m.Id, m.FileName, m.ContentType, m.CreatedAt)).ToList();
    }

    public async Task<MediaInfo?> UploadMediaAsync(Guid postId, string fileName, string contentType, byte[] data)
    {
        var media = new Media
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            FileName = fileName,
            ContentType = contentType,
            Data = data,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _mediaRepo.AddAsync(media);
        return new MediaInfo(created.Id, created.FileName, created.ContentType, created.CreatedAt);
    }

    public async Task<bool> DeleteMediaAsync(Guid mediaId)
    {
        var media = await _mediaRepo.GetByIdAsync(mediaId);
        if (media is null) return false;
        await _mediaRepo.DeleteAsync(media);
        return true;
    }

    public record MediaInfo(Guid Id, string FileName, string ContentType, DateTime CreatedAt);

    // ────────────────────────────── Helpers ───────────────────────────

    private static PostDto MapPost(Post p) => new PostDto(
        p.Id, p.Title, p.Content, p.AuthorId, p.IsPublished, p.CreatedAt, p.PublishedAt,
        p.Author is not null ? new AuthorDto(p.Author.Id, p.Author.UserName) : null,
        0, 0
    );

    private static CommentDto MapComment(Comment c) => new CommentDto(
        c.Id, c.Content, c.PostId, c.UserId, c.ParentCommentId, c.GuestName, c.IsApproved, c.CreatedAt,
        c.User is not null ? new UserRefDto(c.User.Id, c.User.UserName) : null,
        c.Replies.Select(MapComment).ToList()
    );
}
