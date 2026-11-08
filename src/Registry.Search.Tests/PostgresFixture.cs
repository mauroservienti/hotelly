using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Search;
using Testcontainers.PostgreSql;

namespace Registry.Search.Tests;

public class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        Services = new ServiceCollection()
            .AddHotelSearch(container.GetConnectionString(), RegistrySearch.Assembly)
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
