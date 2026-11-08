using Search;

namespace Reservations.Search;

/// <summary>
/// With stay dates, only hotels with no sold-out night during the stay. Not
/// tied to a filter in the request. A hotel with a room left is available:
/// Hotelly doesn't search by room type or number of guests.
/// </summary>
class AvailabilityFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        if (request.Stay is not { } stay)
        {
            return hotels;
        }

        return hotels.Where(id => !db.Set<SoldOutNight>()
            .Any(n => n.HotelId == id && n.Night >= stay.CheckIn && n.Night < stay.CheckOut));
    }
}
