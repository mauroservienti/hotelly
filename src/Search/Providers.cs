namespace Search;

/// <summary>
/// Provides the hotels to search. Implemented by the service that owns the
/// list of hotels: there must be exactly one.
/// </summary>
public interface IHotelsSource
{
    IQueryable<Guid> Hotels(SearchDbContext db);
}

/// <summary>
/// Filters hotels by the criteria a service owns.
/// </summary>
public interface IFilterProvider
{
    IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request);
}

/// <summary>
/// Builds a facet over the filtered hotels, before paging.
/// </summary>
public interface IFacetProvider
{
    Task<Facet> Build(SearchDbContext db, IQueryable<Guid> filtered, SearchRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Sorts hotels by a key a service owns. <paramref name="first"/> tells whether
/// this is the first sort, to use OrderBy, or a following one, to use ThenBy.
/// </summary>
public interface ISortProvider
{
    string Key { get; }

    IOrderedQueryable<Guid> Sort(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request, bool descending, bool first);
}
