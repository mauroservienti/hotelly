using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;

namespace Marketing.Search.Tests;

public class When_sorting_by_rating(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    // Hotel IDs sort in this order, which makes the tie-breaker predictable.
    static readonly Guid Good = Id(1);
    static readonly Guid NoReviews = Id(2);
    static readonly Guid Best = Id(3);
    static readonly Guid Worst = Id(4);
    static readonly Guid SameAsGood = Id(5);

    static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

    public async ValueTask InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE test.hotels, marketing.reviews");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO test.hotels (id) VALUES ({Good}), ({NoReviews}), ({Best}), ({Worst}), ({SameAsGood})");

        (Guid, short)[] reviews =
        [
            (Good, 4), (Good, 4),
            (Best, 5), (Best, 4),
            (Worst, 1), (Worst, 3),
            (SameAsGood, 3), (SameAsGood, 5),
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

    [Fact]
    public async Task Sorts_by_the_average_rating()
    {
        var result = await Search(new SearchRequest { OrderBy = [new SortBy("rating")] });

        // Worst: 2. Good and SameAsGood: 4, the ID breaks the tie. Best: 4.5.
        // NoReviews last.
        Assert.Equal([Worst, Good, SameAsGood, Best, NoReviews], result.HotelIds);
    }

    [Fact]
    public async Task Hotels_without_reviews_come_last_also_descending()
    {
        var result = await Search(new SearchRequest { OrderBy = [new SortBy("rating", Descending: true)] });

        Assert.Equal([Best, Good, SameAsGood, Worst, NoReviews], result.HotelIds);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
