using AspBaseProj.Application.Common;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentsController(
    ICommentRepository commentRepo,
    CurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/comments/post/{postId} — Get all comments for a post.
    /// </summary>
    [HttpGet("post/{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByPost(Guid postId)
    {
        var comments = await commentRepo.GetByPostIdAsync(postId);
        return Ok(comments.Select(AuthHelper.MapComment));
    }

    /// <summary>
    /// POST /api/comments/post/{postId} — Create a new comment on a post.
    /// </summary>
    [HttpPost("post/{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Create(Guid postId, [FromBody] Comment comment)
    {
        comment.Id = Guid.NewGuid();
        comment.PostId = postId;
        comment.CreatedAt = DateTime.UtcNow;

        if (currentUser.IsAuthenticated)
        {
            comment.UserId = currentUser.UserId;
            comment.IsApproved = true;
        }
        else
        {
            comment.IsApproved = false;
            if (string.IsNullOrWhiteSpace(comment.GuestName) || string.IsNullOrWhiteSpace(comment.GuestEmail))
                return BadRequest(new { error = "Guest name and email are required." });
        }

        var created = await commentRepo.AddAsync(comment);
        return Created($"/api/comments/{created.Id}", AuthHelper.MapComment(created));
    }

    /// <summary>
    /// POST /api/comments/{id}/reply — Reply to an existing comment.
    /// </summary>
    [HttpPost("{id:guid}/reply")]
    [AllowAnonymous]
    public async Task<IActionResult> Reply(Guid id, [FromBody] Comment reply)
    {
        var parent = await commentRepo.GetByIdAsync(id);
        if (parent is null) return NotFound();

        reply.Id = Guid.NewGuid();
        reply.PostId = parent.PostId;
        reply.ParentCommentId = id;
        reply.CreatedAt = DateTime.UtcNow;

        if (currentUser.IsAuthenticated)
        {
            reply.UserId = currentUser.UserId;
            reply.IsApproved = true;
        }
        else
        {
            reply.IsApproved = false;
            if (string.IsNullOrWhiteSpace(reply.GuestName) || string.IsNullOrWhiteSpace(reply.GuestEmail))
                return BadRequest(new { error = "Guest name and email are required." });
        }

        var created = await commentRepo.AddAsync(reply);
        return Created($"/api/comments/{created.Id}", AuthHelper.MapComment(created));
    }
}