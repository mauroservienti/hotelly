using Search;

namespace Marketing.Search;

/// <summary>
/// Sorts by the average rating. Hotels without reviews come last, whatever
/// the direction. A plain average: a hotel with a single 5-star review beats
/// one with a hundred reviews averaging 4.9. Real sites weigh the number of
/// reviews; Hotelly is a demo.
/// </summary>
class RatingSort : ISortProvider
{
    public string Key => "rating";

    public IOrderedQueryable<Guid> Sort(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request, bool descending, bool first)
    {
        var missing = descending ? double.MinValue : double.MaxValue;
        return hotels.SortBy(id => db.Set<Review>()
            .Where(r => r.HotelId == id)
            .Average(r => (double?)r.Rating) ?? missing, descending, first);
    }
}
