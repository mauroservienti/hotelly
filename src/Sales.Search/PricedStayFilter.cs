using Search;

namespace Sales.Search;

/// <summary>
/// With stay dates, only hotels with a rate for every night of the stay: a
/// hotel Sales can't price can't be sold. Not tied to a filter in the request.
/// </summary>
class PricedStayFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        if (request.Stay is not { } stay)
        {
            return hotels;
        }

        var nights = stay.CheckOut.DayNumber - stay.CheckIn.DayNumber;
        return hotels.Where(id => db.Set<NightlyRate>()
            .Count(r => r.HotelId == id && r.Night >= stay.CheckIn && r.Night < stay.CheckOut) == nights);
    }
}
