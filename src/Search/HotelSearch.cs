using Microsoft.EntityFrameworkCore;

namespace Search;

/// <summary>
/// The search process: it starts from the hotels, asks each service to filter
/// them, builds the facets, sorts, and pages. It knows nothing about the
/// services' data: providers build the queries, and everything runs in the
/// database. Only the page of IDs, the total count, and the facets come back.
/// </summary>
public class HotelSearch(
    SearchDbContext db,
    IHotelsSource source,
    IEnumerable<IFilterProvider> filters,
    IEnumerable<IFacetProvider> facets,
    IEnumerable<ISortProvider> sorts)
{
    readonly Dictionary<string, ISortProvider> sortsByKey = sorts.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

    public async Task<SearchResult> Search(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var hotels = source.Hotels(db);
        foreach (var filter in filters)
        {
            hotels = filter.Filter(db, hotels, request);
        }

        var builtFacets = new List<Facet>();
        foreach (var facet in facets)
        {
            builtFacets.Add(await facet.Build(db, hotels, request, cancellationToken));
        }

        var totalCount = await hotels.CountAsync(cancellationToken);

        var sorted = hotels;
        var first = true;
        foreach (var sortBy in request.OrderBy)
        {
            if (!sortsByKey.TryGetValue(sortBy.Key, out var sort))
            {
                continue;
            }

            sorted = sort.Sort(db, sorted, request, sortBy.Descending, first);
            first = false;
        }

        // A stable order is required to page: hotels with the same sort keys
        // could otherwise move between pages.
        var ordered = first
            ? sorted.OrderBy(id => id)
            : ((IOrderedQueryable<Guid>)sorted).ThenBy(id => id);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var ids = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SearchResult(ids, totalCount, builtFacets);
    }
}
