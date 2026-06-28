using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/admin/comments")]
[Authorize(Policy = "AdminOrRootPolicy")]
public class ModerationController(
    ICommentRepository commentRepo) : ControllerBase
{
    /// <summary>
    /// GET /api/admin/comments/pending — Get all pending (unapproved) comments.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await commentRepo.GetPendingAsync();
        return Ok(pending.Select(AuthHelper.MapComment));
    }

    /// <summary>
    /// POST /api/admin/comments/{id}/approve — Approve a pending comment.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var comment = await commentRepo.GetByIdAsync(id);
        if (comment is null) return NotFound();

        comment.IsApproved = true;
        await commentRepo.UpdateAsync(comment);
        return Ok(AuthHelper.MapComment(comment));
    }

    /// <summary>
    /// POST /api/admin/comments/{id}/reject — Reject (delete) a pending comment.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var comment = await commentRepo.GetByIdAsync(id);
        if (comment is null) return NotFound();

        await commentRepo.DeleteAsync(comment);
        return NoContent();
    }

    /// <summary>
    /// POST /api/admin/comments/bulk-approve — Bulk approve multiple comments.
    /// </summary>
    [HttpPost("bulk-approve")]
    public async Task<IActionResult> BulkApprove([FromBody] Guid[] ids)
    {
        foreach (var id in ids)
        {
            var comment = await commentRepo.GetByIdAsync(id);
            if (comment is not null) { comment.IsApproved = true; await commentRepo.UpdateAsync(comment); }
        }
        return Ok(new { approved = ids.Length });
    }

    /// <summary>
    /// POST /api/admin/comments/bulk-reject — Bulk reject (delete) multiple comments.
    /// </summary>
    [HttpPost("bulk-reject")]
    public async Task<IActionResult> BulkReject([FromBody] Guid[] ids)
    {
        foreach (var id in ids)
        {
            var comment = await commentRepo.GetByIdAsync(id);
            if (comment is not null) await commentRepo.DeleteAsync(comment);
        }
        return Ok(new { rejected = ids.Length });
    }
}