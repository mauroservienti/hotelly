using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Search.Tests;

public class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    internal SearchDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SearchDbContext>()
                .UseNpgsql(container.GetConnectionString())
                .Options,
            [new TestModel()]);

    internal HotelSearch CreateSearch(SearchDbContext db) =>
        new(db,
            new TestHotelsSource(),
            [new CityFilter(), new AvailabilityFilter()],
            [new CityFacet()],
            [new StarsSort(), new StayPriceSort()]);

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
