using AspBaseProj.Application.Common;
using AspBaseProj.Application.Contracts.Bookmarks;
using AspBaseProj.Application.Contracts.Posts;
using AspBaseProj.Application.Contracts.Ratings;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/posts")]
public class PostsController(
    IPostRepository postRepo,
    IPostRatingRepository ratingRepo,
    IBookmarkRepository bookmarkRepo,
    ICommentRepository commentRepo,
    CurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/posts — Published posts (paginated).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublished([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (posts, total) = await postRepo.GetPublishedAsync(page, pageSize);
        var postIds = posts.Select(p => p.Id).ToList();
        var ratingCounts = await ratingRepo.GetCountsByPostIdsAsync(postIds);
        var dtos = posts.Select(p =>
        {
            var (likeCount, dislikeCount) = ratingCounts.TryGetValue(p.Id, out var rc) ? rc : (0, 0);
            return new PostDto(
                p.Id, p.Title, p.Content, p.AuthorId, p.IsPublished, p.CreatedAt, p.PublishedAt,
                p.Author is not null ? new AuthorDto(p.Author.Id, p.Author.UserName) : null,
                likeCount, dislikeCount
            );
        }).ToList();
        return Ok(new PostListResponse(dtos, total, page, pageSize));
    }

    /// <summary>
    /// GET /api/posts/my — Current user's own posts (paginated).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Policy = "AuthorOrAdminOrRootPolicy")]
    public async Task<IActionResult> GetMyPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Unauthorized();

        var (posts, total) = await postRepo.GetByAuthorIdPaginatedAsync(currentUser.UserId.Value, page, pageSize);

        var postIds = posts.Select(p => p.Id).ToList();
        var commentCounts = await commentRepo.GetCommentCountsByPostIdsAsync(postIds);

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

        return Ok(new MyPostsResponse(dtos, total, page, pageSize));
    }

    /// <summary>
    /// GET /api/posts/{id} — Single post (published or owner/admin/root).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var post = await postRepo.GetByIdAsync(id);
        if (post is null) return NotFound();

        if (post.IsPublished)
            return Ok(AuthHelper.MapPost(post));

        if (currentUser.IsAuthenticated && (post.AuthorId == currentUser.UserId || currentUser.IsRoot || currentUser.IsInGroup("Admin")))
            return Ok(AuthHelper.MapPost(post));

        return NotFound();
    }

    /// <summary>
    /// POST /api/posts — Create a new post.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AuthorOrAdminOrRootPolicy")]
    public async Task<IActionResult> Create([FromBody] Post post)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Unauthorized();

        post.Id = Guid.NewGuid();
        post.AuthorId = currentUser.UserId.Value;
        post.CreatedAt = DateTime.UtcNow;
        if (post.IsPublished) post.PublishedAt = DateTime.UtcNow;

        var created = await postRepo.AddAsync(post);
        return Created($"/api/posts/{created.Id}", AuthHelper.MapPost(created));
    }

    /// <summary>
    /// PUT /api/posts/{id} — Update a post (owner/admin/root only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AuthorOrAdminOrRootPolicy")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Post input)
    {
        var post = await postRepo.GetByIdAsync(id);
        if (post is null) return NotFound();

        if (!currentUser.IsRoot && !currentUser.IsInGroup("Admin") && post.AuthorId != currentUser.UserId)
            return Forbid();

        post.Title = input.Title;
        post.Content = input.Content;
        post.IsPublished = input.IsPublished;
        post.UpdatedAt = DateTime.UtcNow;
        if (input.IsPublished && post.PublishedAt is null) post.PublishedAt = DateTime.UtcNow;

        await postRepo.UpdateAsync(post);
        return Ok(AuthHelper.MapPost(post));
    }

    /// <summary>
    /// DELETE /api/posts/{id} — Delete a post (owner/admin/root only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AuthorOrAdminOrRootPolicy")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var post = await postRepo.GetByIdAsync(id);
        if (post is null) return NotFound();

        if (!currentUser.IsRoot && !currentUser.IsInGroup("Admin") && post.AuthorId != currentUser.UserId)
            return Forbid();

        await postRepo.DeleteAsync(post);
        return NoContent();
    }

    /// <summary>
    /// POST /api/posts/{id}/like — Toggle like on a post.
    /// </summary>
    [HttpPost("{id:guid}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(Guid id)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Unauthorized();

        var post = await postRepo.GetByIdAsync(id);
        if (post is null) return NotFound();

        var existing = await ratingRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
        if (existing is not null && existing.IsLike)
        {
            await ratingRepo.DeleteAsync(existing);
        }
        else if (existing is not null)
        {
            existing.IsLike = true;
            await ratingRepo.UpdateAsync(existing);
        }
        else
        {
            await ratingRepo.AddAsync(new PostRating
            {
                Id = Guid.NewGuid(),
                PostId = id,
                UserId = currentUser.UserId.Value,
                IsLike = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var (likeCount, dislikeCount) = await ratingRepo.GetCountsAsync(id);
        var userRating = await ratingRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
        return Ok(new RatingResponse(likeCount, dislikeCount, userRating is not null ? (userRating.IsLike ? "like" : "dislike") : null));
    }

    /// <summary>
    /// POST /api/posts/{id}/dislike — Toggle dislike on a post.
    /// </summary>
    [HttpPost("{id:guid}/dislike")]
    [Authorize]
    public async Task<IActionResult> ToggleDislike(Guid id)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Unauthorized();

        var post = await postRepo.GetByIdAsync(id);
        if (post is null) return NotFound();

        var existing = await ratingRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
        if (existing is not null && !existing.IsLike)
        {
            await ratingRepo.DeleteAsync(existing);
        }
        else if (existing is not null)
        {
            existing.IsLike = false;
            await ratingRepo.UpdateAsync(existing);
        }
        else
        {
            await ratingRepo.AddAsync(new PostRating
            {
                Id = Guid.NewGuid(),
                PostId = id,
                UserId = currentUser.UserId.Value,
                IsLike = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var (likeCount, dislikeCount) = await ratingRepo.GetCountsAsync(id);
        var userRating = await ratingRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
        return Ok(new RatingResponse(likeCount, dislikeCount, userRating is not null ? (userRating.IsLike ? "like" : "dislike") : null));
    }

    /// <summary>
    /// GET /api/posts/{id}/rating — Get rating status for a post.
    /// </summary>
    [HttpGet("{id:guid}/rating")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRating(Guid id)
    {
        var (likeCount, dislikeCount) = await ratingRepo.GetCountsAsync(id);
        string? userRating = null;
        if (currentUser.IsAuthenticated && currentUser.UserId is not null)
        {
            var existing = await ratingRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
            userRating = existing is not null ? (existing.IsLike ? "like" : "dislike") : null;
        }
        return Ok(new RatingResponse(likeCount, dislikeCount, userRating));
    }

    /// <summary>
    /// POST /api/posts/{id}/bookmark — Toggle bookmark on a post.
    /// </summary>
    [HttpPost("{id:guid}/bookmark")]
    [Authorize]
    public async Task<IActionResult> ToggleBookmark(Guid id)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Unauthorized();

        var post = await postRepo.GetByIdAsync(id);
        if (post is null) return NotFound();

        var existing = await bookmarkRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
        if (existing is not null)
        {
            await bookmarkRepo.DeleteAsync(existing);
            return Ok(new BookmarkToggleResponse(false));
        }

        await bookmarkRepo.AddAsync(new Bookmark
        {
            Id = Guid.NewGuid(),
            PostId = id,
            UserId = currentUser.UserId.Value,
            CreatedAt = DateTime.UtcNow
        });
        return Ok(new BookmarkToggleResponse(true));
    }

    /// <summary>
    /// GET /api/posts/{id}/bookmark — Get bookmark status for a post.
    /// </summary>
    [HttpGet("{id:guid}/bookmark")]
    [Authorize]
    public async Task<IActionResult> GetBookmarkStatus(Guid id)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Ok(new BookmarkStatusResponse(false));

        var existing = await bookmarkRepo.GetByPostAndUserAsync(id, currentUser.UserId.Value);
        return Ok(new BookmarkStatusResponse(existing is not null));
    }

    /// <summary>
    /// GET /api/posts/bookmarks/list — Get all bookmarked posts for current user.
    /// </summary>
    [HttpGet("bookmarks/list")]
    [Authorize]
    public async Task<IActionResult> GetBookmarks([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Unauthorized();

        var (posts, total) = await bookmarkRepo.GetBookmarkedPostsAsync(currentUser.UserId.Value, page, pageSize);

        var postIds = posts.Select(p => p.Id).ToList();
        var ratingCounts = await ratingRepo.GetCountsByPostIdsAsync(postIds);
        var commentCounts = await commentRepo.GetCommentCountsByPostIdsAsync(postIds);

        var dtos = posts.Select(p => new BookmarkedPostDto(
            p.Id,
            p.Title,
            p.Content.Length > 200 ? p.Content[..200] + "..." : p.Content,
            p.Author.UserName,
            p.IsPublished,
            p.PublishedAt,
            ratingCounts.TryGetValue(p.Id, out var rc) ? rc.LikeCount : 0,
            ratingCounts.TryGetValue(p.Id, out rc) ? rc.DislikeCount : 0,
            commentCounts.TryGetValue(p.Id, out var cc) ? cc : 0
        )).ToList();

        return Ok(new BookmarkedPostsResponse(dtos, total, page, pageSize));
    }
}