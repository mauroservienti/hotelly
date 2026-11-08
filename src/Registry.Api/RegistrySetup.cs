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
        // The publication must exist before the subscription: CREATE SUBSCRIPTION
        // asks the publisher which tables to replicate, and records them. Without
        // the publication, the subscription is created with no tables, and stays
        // that way until ALTER SUBSCRIPTION ... REFRESH PUBLICATION.
        await Publish(cancellationToken);
        await Subscribe(cancellationToken);
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

    // Only the columns search needs: no name, no description.
    async Task Publish(CancellationToken cancellationToken)
    {
        await using var command = registry.CreateCommand("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'registry_search') THEN
                    CREATE PUBLICATION registry_search FOR TABLE registry.hotels (id, city, stars, amenities);
                END IF;
            END
            $$
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Runs in the search database, as the registry role: Registry owns its
    // schema there, including the tables and the subscription that fills them.
    async Task Subscribe(CancellationToken cancellationToken)
    {
        await using (var table = search.CreateCommand("""
            CREATE TABLE IF NOT EXISTS registry.hotels (
                id uuid PRIMARY KEY,
                city text NOT NULL,
                stars int NOT NULL,
                amenities text[] NOT NULL
            );
            CREATE INDEX IF NOT EXISTS hotels_city ON registry.hotels (city);
            CREATE INDEX IF NOT EXISTS hotels_amenities ON registry.hotels USING gin (amenities);
            """))
        {
            await table.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var exists = search.CreateCommand("SELECT 1 FROM pg_subscription WHERE subname = 'registry_search'"))
        {
            if (await exists.ExecuteScalarAsync(cancellationToken) is not null)
            {
                return;
            }
        }

        // CREATE SUBSCRIPTION can't run in a transaction block, nor take
        // parameters: the publisher connection comes from configuration.
        var publisher = configuration["Registry:Publisher"]!.Replace("'", "''");
        await using var subscribe = search.CreateCommand(
            $"CREATE SUBSCRIPTION registry_search CONNECTION '{publisher}' PUBLICATION registry_search");
        await subscribe.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Subscribed the search database to Registry's hotels");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
