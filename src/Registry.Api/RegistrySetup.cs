using Hotelly.Replication;
using Hotelly.Seed;
using Npgsql;
using NpgsqlTypes;

namespace Registry.Api;

/// <summary>
/// Sets up Registry's data at startup: its own database, the seed data, and
/// the replication of what search needs to Registry's schema in the search
/// database. Every step is idempotent.
/// </summary>
class RegistrySetup(
    [FromKeyedServices("registry")] NpgsqlDataSource registry,
    [FromKeyedServices("search")] NpgsqlDataSource search,
    IConfiguration configuration,
    ILogger<RegistrySetup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateSchema(cancellationToken);
        await Seed(cancellationToken);

        // Only the columns search needs: no name, no description. The
        // publication must exist before the subscription.
        await SearchReplication.Publish(registry, "registry_search",
            "registry.hotels (id, city, stars, amenities)", cancellationToken);
        await CreateSearchSchema(cancellationToken);
        await SearchReplication.Subscribe(search, "registry_search",
            configuration["Replication:Publisher"]!, "registry_search", cancellationToken);
        logger.LogInformation("Replicating Registry's hotels to the search database");
    }

    async Task CreateSchema(CancellationToken cancellationToken)
    {
        // Logical replication matches tables by schema-qualified name: the
        // registry schema here maps to the registry schema in search.
        await using var command = registry.CreateCommand("""
            CREATE SCHEMA IF NOT EXISTS registry;
            CREATE TABLE IF NOT EXISTS registry.hotels (
                id uuid PRIMARY KEY,
                name text NOT NULL,
                city text NOT NULL,
                stars int NOT NULL,
                amenities text[] NOT NULL,
                description text NOT NULL
            )
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task Seed(CancellationToken cancellationToken)
    {
        await using (var count = registry.CreateCommand("SELECT count(*) FROM registry.hotels"))
        {
            if ((long)(await count.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                return;
            }
        }

        await using var connection = await registry.OpenConnectionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY registry.hotels (id, name, city, stars, amenities, description) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var hotel in RegistrySeed.Hotels())
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(hotel.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(hotel.Name, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(hotel.City, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(hotel.Stars, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(hotel.Amenities, NpgsqlDbType.Array | NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(hotel.Description, NpgsqlDbType.Text, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} hotels", SeedHotels.Count);
    }

    // Runs in the search database, as the registry role: Registry owns its
    // schema there. Same schema-qualified name as the published table.
    async Task CreateSearchSchema(CancellationToken cancellationToken)
    {
        await using var command = search.CreateCommand("""
            CREATE TABLE IF NOT EXISTS registry.hotels (
                id uuid PRIMARY KEY,
                city text NOT NULL,
                stars int NOT NULL,
                amenities text[] NOT NULL
            );
            CREATE INDEX IF NOT EXISTS hotels_city ON registry.hotels (city);
            CREATE INDEX IF NOT EXISTS hotels_amenities ON registry.hotels USING gin (amenities);
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
