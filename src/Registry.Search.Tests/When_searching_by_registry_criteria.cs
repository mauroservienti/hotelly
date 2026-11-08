using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;

namespace Registry.Search.Tests;

public class When_searching_by_registry_criteria(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    // Hotel IDs sort in this order, which makes the tie-breaker predictable.
    static readonly Guid RomeBasic = Id(1);
    static readonly Guid RomeLuxury = Id(2);
    static readonly Guid RomeMid = Id(3);
    static readonly Guid MilanLuxury = Id(4);
    static readonly Guid VeniceMid = Id(5);

    static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

    public async ValueTask InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE registry.hotels");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO registry.hotels (id, city, stars, amenities) VALUES
            ({RomeBasic}, 'Rome', 2, ARRAY['wifi']),
            ({RomeLuxury}, 'Rome', 5, ARRAY['wifi', 'pool', 'spa']),
            ({RomeMid}, 'Rome', 3, ARRAY['wifi', 'parking']),
            ({MilanLuxury}, 'Milan', 5, ARRAY['wifi', 'spa']),
            ({VeniceMid}, 'Venice', 3, ARRAY['parking'])
            """);
    }

    async Task<SearchResult> Search(SearchRequest request)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HotelSearch>()
            .Search(request, TestContext.Current.CancellationToken);
    }

    static SearchRequest Filter(string name, params string[] values) =>
        new() { Filters = new Dictionary<string, string[]> { [name] = values } };

    [Fact]
    public async Task Filters_by_any_of_the_cities()
    {
        var result = await Search(Filter("city", "Milan", "Venice"));

        Assert.Equal([MilanLuxury, VeniceMid], result.HotelIds);
    }

    [Fact]
    public async Task Filters_by_any_of_the_stars()
    {
        var result = await Search(Filter("stars", "3", "5"));

        Assert.Equal([RomeLuxury, RomeMid, MilanLuxury, VeniceMid], result.HotelIds);
    }

    [Fact]
    public async Task Ignores_stars_that_are_not_numbers()
    {
        var result = await Search(Filter("stars", "lots"));

        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Filters_by_all_the_amenities()
    {
        var result = await Search(Filter("amenities", "wifi", "spa"));

        Assert.Equal([RomeLuxury, MilanLuxury], result.HotelIds);
    }

    [Fact]
    public async Task Facets_count_the_filtered_hotels()
    {
        var result = await Search(Filter("city", "Rome"));

        Assert.Equal([new FacetValue("Rome", 3)], Facet(result, "city"));
        Assert.Equal([new FacetValue("5", 1), new FacetValue("3", 1), new FacetValue("2", 1)], Facet(result, "stars"));
        Assert.Equal(
            [new FacetValue("wifi", 3), new FacetValue("parking", 1), new FacetValue("pool", 1), new FacetValue("spa", 1)],
            Facet(result, "amenities"));
    }

    [Fact]
    public async Task Sorts_by_stars()
    {
        var result = await Search(new SearchRequest { OrderBy = [new SortBy("stars", Descending: true)] });

        Assert.Equal([RomeLuxury, MilanLuxury, RomeMid, VeniceMid, RomeBasic], result.HotelIds);
    }

    static IReadOnlyList<FacetValue> Facet(SearchResult result, string name) =>
        Assert.Single(result.Facets, f => f.Name == name).Values;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
