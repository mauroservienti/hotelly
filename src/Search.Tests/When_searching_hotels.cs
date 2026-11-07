using Microsoft.EntityFrameworkCore;

namespace Search.Tests;

public class When_searching_hotels(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    static readonly DateOnly CheckIn = new(2027, 2, 10);
    static readonly StayDates Stay = new(CheckIn, CheckIn.AddDays(3));

    // Hotel IDs sort in this order, which makes the tie-breaker predictable.
    static readonly Guid RomeCheap = Id(1);
    static readonly Guid RomeExpensive = Id(2);
    static readonly Guid RomeSoldOut = Id(3);
    static readonly Guid RomeSameAsCheap = Id(4);
    static readonly Guid MilanMid = Id(5);
    static readonly Guid MilanTop = Id(6);
    static readonly Guid Florence = Id(7);

    static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE registry.hotels, reservations.sold_out_nights, sales.nightly_rates");

        db.AddRange(
            Hotel(RomeCheap, "Rome", 3),
            Hotel(RomeExpensive, "Rome", 5),
            Hotel(RomeSoldOut, "Rome", 4),
            Hotel(RomeSameAsCheap, "Rome", 3),
            Hotel(MilanMid, "Milan", 4),
            Hotel(MilanTop, "Milan", 5),
            Hotel(Florence, "Florence", 4));

        // Rates for the three nights of the stay. Seasonal prices: the second
        // night costs more, so the stay price is a sum, not a nightly rate.
        Rates(db, RomeCheap, 100, 150, 100);
        Rates(db, RomeExpensive, 300, 300, 300);
        Rates(db, RomeSoldOut, 50, 50, 50);
        Rates(db, RomeSameAsCheap, 120, 110, 120);
        Rates(db, MilanMid, 200, 200, 200);
        Rates(db, MilanTop, 400, 400, 400);
        Rates(db, Florence, 180, 180, 180);

        db.Add(new SoldOutNight { HotelId = RomeSoldOut, Night = CheckIn.AddDays(1) });

        await db.SaveChangesAsync();
    }

    static TestHotel Hotel(Guid id, string city, int stars) => new() { Id = id, City = city, Stars = stars };

    static void Rates(SearchDbContext db, Guid hotelId, params decimal[] rates)
    {
        for (var i = 0; i < rates.Length; i++)
        {
            db.Add(new NightlyRate { HotelId = hotelId, Night = CheckIn.AddDays(i), Rate = rates[i] });
        }
    }

    async Task<SearchResult> Search(SearchRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await fixture.CreateSearch(db).Search(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Filters_owned_by_different_services_are_combined()
    {
        var result = await Search(new SearchRequest
        {
            Stay = Stay,
            Filters = new Dictionary<string, string[]> { ["city"] = ["Rome"] }
        });

        Assert.Equal([RomeCheap, RomeExpensive, RomeSameAsCheap], result.HotelIds);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Facets_count_all_filtered_hotels_not_only_the_page()
    {
        var result = await Search(new SearchRequest { Stay = Stay, PageSize = 2 });

        var city = Assert.Single(result.Facets);
        Assert.Equal(
            [new FacetValue("Florence", 1), new FacetValue("Milan", 2), new FacetValue("Rome", 3)],
            city.Values);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.HotelIds.Count);
    }

    [Fact]
    public async Task Sorting_by_stay_price_happens_before_paging()
    {
        var request = new SearchRequest { Stay = Stay, OrderBy = [new SortBy("price")], PageSize = 2 };

        var page1 = await Search(request);
        var page2 = await Search(request with { Page = 2 });
        var page3 = await Search(request with { Page = 3 });

        // RomeCheap and RomeSameAsCheap both cost 350: the hotel ID breaks the tie.
        Assert.Equal([RomeCheap, RomeSameAsCheap], page1.HotelIds);
        Assert.Equal([Florence, MilanMid], page2.HotelIds);
        Assert.Equal([RomeExpensive, MilanTop], page3.HotelIds);
    }

    [Fact]
    public async Task Sorts_owned_by_different_services_are_combined()
    {
        var result = await Search(new SearchRequest
        {
            Stay = Stay,
            OrderBy = [new SortBy("stars", Descending: true), new SortBy("price")]
        });

        Assert.Equal([RomeExpensive, MilanTop, Florence, MilanMid, RomeCheap, RomeSameAsCheap], result.HotelIds);
    }

    [Fact]
    public async Task Unknown_sorts_are_ignored()
    {
        var result = await Search(new SearchRequest { OrderBy = [new SortBy("distance")] });

        Assert.Equal([RomeCheap, RomeExpensive, RomeSoldOut, RomeSameAsCheap, MilanMid, MilanTop, Florence], result.HotelIds);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
