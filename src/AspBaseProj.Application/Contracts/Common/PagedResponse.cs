namespace AspBaseProj.Application.Contracts.Common;

public record PagedResponse<T>(List<T> Items, int Total, int Page, int PageSize);