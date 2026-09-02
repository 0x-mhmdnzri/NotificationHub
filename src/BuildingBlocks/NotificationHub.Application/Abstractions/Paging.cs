namespace NotificationHub.Application.Abstractions;

/// <summary>
/// Canonical server-side table query parameters.
/// page is 1-based. sort is a field name; order is asc|desc.
/// </summary>
public sealed record PagedRequest(
    int Page = 1,
    int PageSize = 20,
    string? Sort = null,
    string? Order = "asc",
    string? Search = null)
{
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize < 1 ? 20 : PageSize > 100 ? 100 : PageSize;
    public int Skip => (SafePage - 1) * SafePageSize;
    public bool Descending => string.Equals(Order, "desc", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Canonical server-side table response envelope.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page * PageSize < TotalCount;
    public bool HasPrevious => Page > 1;

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new([], page, pageSize, 0);
}
