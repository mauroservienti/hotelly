using System.Linq.Expressions;

namespace Search;

public static class SortExtensions
{
    /// <summary>
    /// Applies OrderBy, OrderByDescending, ThenBy, or ThenByDescending,
    /// depending on whether this is the first sort and on the direction.
    /// </summary>
    public static IOrderedQueryable<Guid> SortBy<TKey>(
        this IQueryable<Guid> hotels,
        Expression<Func<Guid, TKey>> key,
        bool descending,
        bool first) =>
        (first, descending) switch
        {
            (true, false) => hotels.OrderBy(key),
            (true, true) => hotels.OrderByDescending(key),
            (false, false) => ((IOrderedQueryable<Guid>)hotels).ThenBy(key),
            (false, true) => ((IOrderedQueryable<Guid>)hotels).ThenByDescending(key),
        };
}
