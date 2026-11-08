namespace Search;

/// <summary>
/// A search request. Criteria are owned by different services: each provider
/// reads only the filters it owns, and ignores the others.
/// </summary>
public record SearchRequest
{
    /// <summary>
    /// Check-in and check-out dates, if any. Without them, there's no
    /// availability to check and no stay price to compute.
    /// </summary>
    public StayDates? Stay { get; init; }

    /// <summary>
    /// Filters by name, for example "city" or "stars". Values are strings,
    /// the same way they come from a query string.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Filters { get; init; } = new Dictionary<string, string[]>();

    /// <summary>
    /// Sorts in order of priority. Each key is handled by a sort provider.
    /// </summary>
    public IReadOnlyList<SortBy> OrderBy { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string[] GetFilter(string name) =>
        Filters.TryGetValue(name, out var values) ? values : [];
}

public record StayDates(DateOnly CheckIn, DateOnly CheckOut);

public record SortBy(string Key, bool Descending = false);
