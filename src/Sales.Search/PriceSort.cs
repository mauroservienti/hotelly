using Search;

namespace Sales.Search;

/// <summary>
/// With stay dates, sorts by the price of the stay: the sum of the nightly
/// rates, which change with the season. Without stay dates, sorts by the
/// lowest nightly rate in the next 30 days, the "from" price hotel listings
/// usually show.
/// </summary>
class PriceSort(TimeProvider time) : ISortProvider
{
    public const int FromPriceDays = 30;

    public string Key => "price";

    public IOrderedQueryable<Guid> Sort(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request, bool descending, bool first)
    {
        if (request.Stay is { } stay)
        {
            return hotels.SortBy(id => db.Set<NightlyRate>()
                .Where(r => r.HotelId == id && r.Night >= stay.CheckIn && r.Night < stay.CheckOut)
                .Sum(r => r.Rate), descending, first);
        }

        // Hotels without a rate in the next 30 days come last, whatever the
        // direction: PostgreSQL would otherwise put them first when descending.
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var until = today.AddDays(FromPriceDays);
        var missing = descending ? decimal.MinValue : decimal.MaxValue;
        return hotels.SortBy(id => db.Set<NightlyRate>()
            .Where(r => r.HotelId == id && r.Night >= today && r.Night < until)
            .Min(r => (decimal?)r.Rate) ?? missing, descending, first);
    }
}
