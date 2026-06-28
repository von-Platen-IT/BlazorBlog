using AspBaseProj.Application.Contracts.Settings;
using AspBaseProj.Domain.Entities;
using AspBaseProj.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "RootPolicy")]
public class SettingsController(
    ISystemSettingRepository settingRepo) : ControllerBase
{
    /// <summary>
    /// GET /api/admin/settings — Get all system settings.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await settingRepo.GetAllAsync();
        return Ok(settings.Select(s => new SettingDto(s.Key, s.Value, s.UpdatedAt)));
    }

    /// <summary>
    /// PUT /api/admin/settings/{key} — Update a system setting.
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest request)
    {
        await settingRepo.SetAsync(new SystemSetting { Key = key, Value = request.Value });
        return Ok(new { key, request.Value });
    }
}