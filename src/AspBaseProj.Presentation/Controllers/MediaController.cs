using AspBaseProj.Application.Common;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController(
    IMediaRepository mediaRepo,
    IPostRepository postRepo,
    CurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/media/post/{postId} — Get media metadata for a post.
    /// </summary>
    [HttpGet("post/{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByPost(Guid postId)
    {
        var media = await mediaRepo.GetByPostIdAsync(postId);
        return Ok(media.Select(m => new { m.Id, m.FileName, m.ContentType, m.CreatedAt }));
    }

    /// <summary>
    /// POST /api/media/post/{postId} — Upload an image for a post.
    /// </summary>
    [HttpPost("post/{postId:guid}")]
    [Authorize(Policy = "AuthorOrAdminOrRootPolicy")]
    public async Task<IActionResult> Upload(Guid postId)
    {
        var post = await postRepo.GetByIdAsync(postId);
        if (post is null) return NotFound();

        if (!currentUser.IsRoot && !currentUser.IsInGroup("Admin") && post.AuthorId != currentUser.UserId)
            return Forbid();

        var form = await Request.ReadFormAsync();
        var file = form.Files.GetFile("file");
        if (file is null) return BadRequest(new { error = "No file provided." });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Invalid file type." });
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "File too large. Max 5 MB." });

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
        await mediaRepo.AddAsync(mediaItem);
        return Created($"/api/media/{mediaItem.Id}", new { mediaItem.Id, mediaItem.FileName, mediaItem.ContentType });
    }

    /// <summary>
    /// GET /api/media/{id}/data — Get the raw image data.
    /// </summary>
    [HttpGet("{id:guid}/data")]
    [AllowAnonymous]
    public async Task<IActionResult> GetData(Guid id)
    {
        var media = await mediaRepo.GetByIdAsync(id);
        return media is not null ? File(media.Data, media.ContentType, media.FileName) : NotFound();
    }

    /// <summary>
    /// DELETE /api/media/{id} — Delete a media item (owner/admin/root only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AuthorOrAdminOrRootPolicy")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var media = await mediaRepo.GetByIdAsync(id);
        if (media is null) return NotFound();

        var post = await postRepo.GetByIdAsync(media.PostId);
        if (post is null) return NotFound();

        if (!currentUser.IsRoot && !currentUser.IsInGroup("Admin") && post.AuthorId != currentUser.UserId)
            return Forbid();

        await mediaRepo.DeleteAsync(media);
        return NoContent();
    }
}