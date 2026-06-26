namespace Kantonal.Application;

public sealed record FinanceListRequest(
    string? Municipality,
    int? Year,
    string? SortBy,
    string? SortDir,
    int Page,
    int PageSize);
