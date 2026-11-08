using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;
using Testcontainers.PostgreSql;

namespace Sales.Search.Tests;

public class PostgresFixture : IAsyncLifetime
{
    public static readonly DateOnly Today = new(2027, 2, 1);

    readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        // Sales doesn't own the list of hotels: the tests provide one.
        Services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(Today))
            .AddHotelSearch(container.GetConnectionString(), SalesSearch.Assembly, typeof(PostgresFixture).Assembly)
            .BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await container.DisposeAsync();
    }
}

class FixedTimeProvider(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

class TestHotel
{
    public Guid Id { get; set; }
}

class TestHotels : ISearchModelContributor, IHotelsSource
{
    public void Contribute(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<TestHotel>(e =>
        {
            e.ToTable("hotels", "test");
            e.Property(h => h.Id).HasColumnName("id");
        });

    public IQueryable<Guid> Hotels(SearchDbContext db) => db.Set<TestHotel>().Select(h => h.Id);
}
