namespace Kantonal.Application;

public sealed record FinanceFilterOptions(
    IReadOnlyList<string> Municipalities,
    IReadOnlyList<int> Years);
