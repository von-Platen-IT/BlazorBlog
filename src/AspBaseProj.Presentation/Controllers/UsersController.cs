using AspBaseProj.Application.Contracts.Users;
using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RootPolicy")]
public class UsersController(
    IAppUserRepository userRepo,
    IGroupRepository groupRepo) : ControllerBase
{
    /// <summary>
    /// GET /api/admin/groups — Get all groups.
    /// </summary>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await groupRepo.GetAllAsync();
        return Ok(groups.Select(g => new GroupDto(g.Id, g.Name, g.Description)));
    }

    /// <summary>
    /// GET /api/admin/users — Get all users.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await userRepo.GetAllAsync();
        return Ok(users.Select(u => new UserDto(
            u.Id, u.UserName, u.Email, u.IsRoot, u.CreatedAt,
            u.Groups.Select(g => g.Name).ToList()
        )));
    }

    /// <summary>
    /// GET /api/admin/users/{id} — Get a single user by ID.
    /// </summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await userRepo.GetByIdAsync(id);
        return user is not null ? Ok(new UserDto(
            user.Id, user.UserName, user.Email, user.IsRoot, user.CreatedAt,
            user.Groups.Select(g => g.Name).ToList()
        )) : NotFound();
    }

    /// <summary>
    /// POST /api/admin/users/{id}/groups — Assign a group to a user.
    /// </summary>
    [HttpPost("users/{id:guid}/groups")]
    public async Task<IActionResult> AssignGroup(Guid id, [FromBody] Guid groupId)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();
        if (user.IsRoot) return BadRequest(new { error = "Cannot modify root user." });

        var group = await groupRepo.GetByIdAsync(groupId);
        if (group is null) return NotFound();

        if (!user.Groups.Any(g => g.Id == groupId))
        {
            user.Groups.Add(group);
            await userRepo.UpdateAsync(user);
        }
        return Ok(new { user.Id, Groups = user.Groups.Select(g => g.Name).ToList() });
    }

    /// <summary>
    /// DELETE /api/admin/users/{id}/groups/{groupId} — Remove a group from a user.
    /// </summary>
    [HttpDelete("users/{id:guid}/groups/{groupId:guid}")]
    public async Task<IActionResult> RemoveGroup(Guid id, Guid groupId)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();
        if (user.IsRoot) return BadRequest(new { error = "Cannot modify root user." });

        var group = user.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is not null)
        {
            user.Groups.Remove(group);
            await userRepo.UpdateAsync(user);
        }
        return Ok(new { user.Id, Groups = user.Groups.Select(g => g.Name).ToList() });
    }

    /// <summary>
    /// DELETE /api/admin/users/{id} — Delete a user (cannot delete root).
    /// </summary>
    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();
        if (user.IsRoot) return BadRequest(new { error = "Cannot delete root user." });

        await userRepo.DeleteAsync(user);
        return NoContent();
    }
}