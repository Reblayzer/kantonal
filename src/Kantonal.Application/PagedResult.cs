namespace Kantonal.Application;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
