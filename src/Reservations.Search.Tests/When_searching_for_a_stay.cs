using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;

namespace Reservations.Search.Tests;

public class When_searching_for_a_stay(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    static readonly DateOnly CheckIn = new(2027, 2, 12);
    static readonly StayDates Stay = new(CheckIn, CheckIn.AddDays(3));

    static readonly Guid Available = Id(1);
    static readonly Guid SoldOutFirstNight = Id(2);
    static readonly Guid SoldOutLastNight = Id(3);
    static readonly Guid SoldOutBefore = Id(4);
    static readonly Guid SoldOutOnCheckOut = Id(5);

    static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

    public async ValueTask InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE test.hotels, reservations.availability");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO test.hotels (id) VALUES ({Available}), ({SoldOutFirstNight}), ({SoldOutLastNight}), ({SoldOutBefore}), ({SoldOutOnCheckOut})");

        // Only sold-out nights are replicated to search.
        (Guid, DateOnly)[] soldOut =
        [
            (SoldOutFirstNight, CheckIn),
            (SoldOutLastNight, CheckIn.AddDays(2)),
            (SoldOutBefore, CheckIn.AddDays(-1)),
            // Check-out day: the guest doesn't stay that night.
            (SoldOutOnCheckOut, CheckIn.AddDays(3)),
        ];
        foreach (var (hotelId, night) in soldOut)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO reservations.availability (hotel_id, night) VALUES ({hotelId}, {night})");
        }
    }

    async Task<SearchResult> Search(SearchRequest request)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HotelSearch>()
            .Search(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Hotels_with_a_sold_out_night_during_the_stay_are_excluded()
    {
        var result = await Search(new SearchRequest { Stay = Stay });

        Assert.Equal([Available, SoldOutBefore, SoldOutOnCheckOut], result.HotelIds);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Without_stay_dates_all_hotels_are_returned()
    {
        var result = await Search(new SearchRequest());

        Assert.Equal(5, result.TotalCount);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
