using Microsoft.EntityFrameworkCore;

namespace Search.Tests;

// Test doubles of the services' search tables and providers. The real ones
// live in each service.

class TestHotel
{
    public Guid Id { get; set; }
    public string City { get; set; } = "";
    public int Stars { get; set; }
}

class SoldOutNight
{
    public Guid HotelId { get; set; }
    public DateOnly Night { get; set; }
}

class NightlyRate
{
    public Guid HotelId { get; set; }
    public DateOnly Night { get; set; }
    public decimal Rate { get; set; }
}

class TestModel : ISearchModelContributor
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestHotel>(e =>
        {
            e.ToTable("hotels", "registry");
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.City).HasColumnName("city");
            e.Property(h => h.Stars).HasColumnName("stars");
        });
        modelBuilder.Entity<SoldOutNight>(e =>
        {
            e.ToTable("sold_out_nights", "reservations");
            e.HasKey(n => new { n.HotelId, n.Night });
            e.Property(n => n.HotelId).HasColumnName("hotel_id");
            e.Property(n => n.Night).HasColumnName("night");
        });
        modelBuilder.Entity<NightlyRate>(e =>
        {
            e.ToTable("nightly_rates", "sales");
            e.HasKey(r => new { r.HotelId, r.Night });
            e.Property(r => r.HotelId).HasColumnName("hotel_id");
            e.Property(r => r.Night).HasColumnName("night");
            e.Property(r => r.Rate).HasColumnName("rate");
        });
    }
}

class TestHotelsSource : IHotelsSource
{
    public IQueryable<Guid> Hotels(SearchDbContext db) => db.Set<TestHotel>().Select(h => h.Id);
}

class CityFilter : IFilterProvider
{
    public IQueryable<Guid> Filter(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request)
    {
        var cities = request.GetFilter("city");
        return cities.Length == 0
            ? hotels
            : hotels.Where(id => db.Set<TestHotel>().Any(h => h.Id == id && cities.Contains(h.City)));
    }
}

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

class CityFacet : IFacetProvider
{
    public async Task<Facet> Build(SearchDbContext db, IQueryable<Guid> filtered, SearchRequest request, CancellationToken cancellationToken)
    {
        var values = await db.Set<TestHotel>()
            .Where(h => filtered.Contains(h.Id))
            .GroupBy(h => h.City)
            .OrderBy(g => g.Key)
            .Select(g => new FacetValue(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return new Facet("city", values);
    }
}

class StarsSort : ISortProvider
{
    public string Key => "stars";

    public IOrderedQueryable<Guid> Sort(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request, bool descending, bool first) =>
        hotels.SortBy(id => db.Set<TestHotel>().Where(h => h.Id == id).Select(h => h.Stars).First(), descending, first);
}

class StayPriceSort : ISortProvider
{
    public string Key => "price";

    public IOrderedQueryable<Guid> Sort(SearchDbContext db, IQueryable<Guid> hotels, SearchRequest request, bool descending, bool first)
    {
        var stay = request.Stay ?? throw new InvalidOperationException("Sorting by price requires stay dates.");

        return hotels.SortBy(id => db.Set<NightlyRate>()
            .Where(r => r.HotelId == id && r.Night >= stay.CheckIn && r.Night < stay.CheckOut)
            .Sum(r => r.Rate), descending, first);
    }
}
