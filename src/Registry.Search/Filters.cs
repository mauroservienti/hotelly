using Search;

namespace Registry.Search;

/// <summary>
/// Hotels in any of the requested cities.
/// </summary>
class CityFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        var cities = request.GetFilter("city");
        return cities.Length == 0
            ? hotels
            : hotels.Where(id => db.Set<RegistryHotel>().Any(h => h.Id == id && cities.Contains(h.City)));
    }
}

/// <summary>
/// Hotels with any of the requested numbers of stars.
/// </summary>
class StarsFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        var stars = request.GetFilter("stars")
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .OfType<int>()
            .ToArray();
        return stars.Length == 0
            ? hotels
            : hotels.Where(id => db.Set<RegistryHotel>().Any(h => h.Id == id && stars.Contains(h.Stars)));
    }
}

/// <summary>
/// Hotels with all the requested amenities.
/// </summary>
class AmenitiesFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        var amenities = request.GetFilter("amenities");
        return amenities.Length == 0
            ? hotels
            : hotels.Where(id => db.Set<RegistryHotel>().Any(h => h.Id == id && amenities.All(a => h.Amenities.Contains(a))));
    }
}
