using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;
using Testcontainers.PostgreSql;

namespace Reservations.Search.Tests;

public class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        // Reservations doesn't own the list of hotels: the tests provide one.
        Services = new ServiceCollection()
            .AddHotelSearch(container.GetConnectionString(), ReservationsSearch.Assembly, typeof(PostgresFixture).Assembly)
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
