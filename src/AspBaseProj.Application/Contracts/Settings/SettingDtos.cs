namespace AspBaseProj.Application.Contracts.Settings;

public record SettingDto(string Key, string? Value, DateTime? UpdatedAt);

public record UpdateSettingRequest(string Value);