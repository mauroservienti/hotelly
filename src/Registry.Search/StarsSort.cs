using Search;

namespace Registry.Search;

class StarsSort : ISortProvider
{
    public string Key => "stars";

    public IOrderedQueryable<Guid> Sort(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request, bool descending, bool first) =>
        hotels.SortBy(id => db.Set<RegistryHotel>().Where(h => h.Id == id).Select(h => h.Stars).First(), descending, first);
}
