using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;

namespace Sales.Search.Tests;

public class When_searching_by_price(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    // Beyond the 30 days of the "from" price, so the two don't mix.
    static readonly DateOnly CheckIn = PostgresFixture.Today.AddDays(40);
    static readonly StayDates Stay = new(CheckIn, CheckIn.AddDays(3));

    // Hotel IDs sort in this order, which makes the tie-breaker predictable.
    static readonly Guid FlatRate = Id(1);
    static readonly Guid PeakNight = Id(2);
    static readonly Guid Cheapest = Id(3);
    static readonly Guid MissingNight = Id(4);
    static readonly Guid SameAsFlat = Id(5);

    static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

    public async ValueTask InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE test.hotels, sales.nightly_rates");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO test.hotels (id) VALUES ({FlatRate}), ({PeakNight}), ({Cheapest}), ({MissingNight}), ({SameAsFlat})");

        // Nightly rates for the three nights of the stay, plus the night
        // after, which isn't part of it.
        var rates = new List<(Guid, DateOnly, decimal)>();
        Rates(rates, FlatRate, 100, 100, 100, 1);
        Rates(rates, PeakNight, 80, 200, 80, 1);
        Rates(rates, Cheapest, 90, 90, 90, 1000);
        Rates(rates, MissingNight, 10, 10);
        Rates(rates, SameAsFlat, 110, 90, 100, 1);

        // Rates for the "from" price, outside the stay.
        rates.Add((FlatRate, PostgresFixture.Today, 50));
        rates.Add((Cheapest, PostgresFixture.Today.AddDays(1), 70));
        rates.Add((PeakNight, PostgresFixture.Today.AddDays(29), 60));
        // Before today, or more than 30 days ahead: ignored.
        rates.Add((PeakNight, PostgresFixture.Today.AddDays(-1), 5));
        rates.Add((SameAsFlat, PostgresFixture.Today.AddDays(30), 5));

        foreach (var (hotelId, night, rate) in rates)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO sales.nightly_rates (hotel_id, night, rate) VALUES ({hotelId}, {night}, {rate})");
        }
    }

    static void Rates(List<(Guid, DateOnly, decimal)> rates, Guid hotelId, params decimal[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            rates.Add((hotelId, CheckIn.AddDays(i), values[i]));
        }
    }

    async Task<SearchResult> Search(SearchRequest request)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HotelSearch>()
            .Search(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Hotels_without_a_rate_for_every_night_are_excluded()
    {
        var result = await Search(new SearchRequest { Stay = Stay });

        Assert.DoesNotContain(MissingNight, result.HotelIds);
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public async Task Sorts_by_the_price_of_the_stay()
    {
        var result = await Search(new SearchRequest { Stay = Stay, OrderBy = [new SortBy("price")] });

        // Cheapest: 270. FlatRate and SameAsFlat: 300, the ID breaks the tie.
        // PeakNight: 360, because of its second night.
        Assert.Equal([Cheapest, FlatRate, SameAsFlat, PeakNight], result.HotelIds);
    }

    [Fact]
    public async Task Sorts_by_the_price_of_the_stay_descending()
    {
        var result = await Search(new SearchRequest { Stay = Stay, OrderBy = [new SortBy("price", Descending: true)] });

        Assert.Equal([PeakNight, FlatRate, SameAsFlat, Cheapest], result.HotelIds);
    }

    [Fact]
    public async Task Without_stay_dates_sorts_by_the_lowest_rate_in_the_next_30_days()
    {
        var result = await Search(new SearchRequest { OrderBy = [new SortBy("price")] });

        // FlatRate: 50, PeakNight: 60, Cheapest: 70. MissingNight and
        // SameAsFlat have no rate in the next 30 days: last, by ID.
        Assert.Equal([FlatRate, PeakNight, Cheapest, MissingNight, SameAsFlat], result.HotelIds);
    }

    [Fact]
    public async Task Without_stay_dates_hotels_without_a_from_price_come_last_also_descending()
    {
        var result = await Search(new SearchRequest { OrderBy = [new SortBy("price", Descending: true)] });

        Assert.Equal([Cheapest, PeakNight, FlatRate, MissingNight, SameAsFlat], result.HotelIds);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
