using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;

namespace Marketing.Search.Tests;

public class When_filtering_and_faceting_by_rating(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    static readonly Guid FourAndAHalf = Id(1);
    static readonly Guid Four = Id(2);
    static readonly Guid ThreeAndAHalf = Id(3);
    static readonly Guid Two = Id(4);
    static readonly Guid OneAndAHalf = Id(5);
    static readonly Guid NoReviews = Id(6);

    static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

    public async ValueTask InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE test.hotels, marketing.reviews");
        foreach (var id in new[] { FourAndAHalf, Four, ThreeAndAHalf, Two, OneAndAHalf, NoReviews })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO test.hotels (id) VALUES ({id})");
        }

        (Guid, short)[] reviews =
        [
            (FourAndAHalf, 5), (FourAndAHalf, 4),
            (Four, 4),
            (ThreeAndAHalf, 3), (ThreeAndAHalf, 4),
            (Two, 1), (Two, 3),
            (OneAndAHalf, 1), (OneAndAHalf, 2),
        ];
        foreach (var (hotelId, rating) in reviews)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO marketing.reviews (id, hotel_id, rating) VALUES ({Guid.NewGuid()}, {hotelId}, {rating})");
        }
    }

    async Task<SearchResult> Search(SearchRequest request)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HotelSearch>()
            .Search(request, TestContext.Current.CancellationToken);
    }

    static SearchRequest Rating(params string[] values) =>
        new() { Filters = new Dictionary<string, string[]> { ["rating"] = values } };

    [Fact]
    public async Task Keeps_hotels_rated_at_least_the_requested_value()
    {
        var result = await Search(Rating("4"));

        Assert.Equal([FourAndAHalf, Four], result.HotelIds);
    }

    [Fact]
    public async Task With_more_than_one_value_the_lowest_wins()
    {
        var result = await Search(Rating("4", "3"));

        Assert.Equal([FourAndAHalf, Four, ThreeAndAHalf], result.HotelIds);
    }

    [Fact]
    public async Task Counts_hotels_in_overlapping_bands()
    {
        var result = await Search(new SearchRequest());

        var facet = Assert.Single(result.Facets, f => f.Name == "rating");
        Assert.Equal([new("4", 2), new("3", 3), new("2", 4)], facet.Values);
    }

    [Fact]
    public async Task Counts_only_the_filtered_hotels()
    {
        var result = await Search(Rating("3"));

        var facet = Assert.Single(result.Facets, f => f.Name == "rating");
        Assert.Equal([new("4", 2), new("3", 3), new("2", 3)], facet.Values);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
