using Search;

namespace Marketing.Search;

/// <summary>
/// Hotels rated at least the requested value on average: <c>rating=4</c>
/// means "4 and up", like the rating facet's bands. With more than one value,
/// the lowest wins, the same "any of" the other filters use. Hotels without
/// reviews never match.
/// </summary>
class RatingFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        var minimums = request.GetFilter("rating")
            .Select(r => double.TryParse(r, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : (double?)null)
            .OfType<double>()
            .ToArray();
        if (minimums.Length == 0)
        {
            return hotels;
        }

        var minimum = minimums.Min();
        return hotels.Where(id => db.Set<Review>()
            .Where(r => r.HotelId == id)
            .Average(r => (double?)r.Rating) >= minimum);
    }
}
