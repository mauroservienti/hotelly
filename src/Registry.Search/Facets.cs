using Microsoft.EntityFrameworkCore;
using Search;

namespace Registry.Search;

class CityFacet : IFacetProvider
{
    public async Task<Facet> Build(SearchDbContext db, IQueryable<Guid> filtered, SearchRequest request, CancellationToken cancellationToken)
    {
        var values = await db.Set<RegistryHotel>()
            .Where(h => filtered.Contains(h.Id))
            .GroupBy(h => h.City)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(v => v.Count).ThenBy(v => v.Value)
            .ToListAsync(cancellationToken);

        return new Facet("city", values.Select(v => new FacetValue(v.Value, v.Count)).ToList());
    }
}

class StarsFacet : IFacetProvider
{
    public async Task<Facet> Build(SearchDbContext db, IQueryable<Guid> filtered, SearchRequest request, CancellationToken cancellationToken)
    {
        var values = await db.Set<RegistryHotel>()
            .Where(h => filtered.Contains(h.Id))
            .GroupBy(h => h.Stars)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(v => v.Value)
            .ToListAsync(cancellationToken);

        return new Facet("stars", values.Select(v => new FacetValue(v.Value.ToString(), v.Count)).ToList());
    }
}

class AmenitiesFacet : IFacetProvider
{
    public async Task<Facet> Build(SearchDbContext db, IQueryable<Guid> filtered, SearchRequest request, CancellationToken cancellationToken)
    {
        var values = await db.Set<RegistryHotel>()
            .Where(h => filtered.Contains(h.Id))
            .SelectMany(h => h.Amenities)
            .GroupBy(a => a)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(v => v.Count).ThenBy(v => v.Value)
            .ToListAsync(cancellationToken);

        return new Facet("amenities", values.Select(v => new FacetValue(v.Value, v.Count)).ToList());
    }
}
