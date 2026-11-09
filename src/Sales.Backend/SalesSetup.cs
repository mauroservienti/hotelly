using Hotelly.Replication;
using Npgsql;
using NpgsqlTypes;

namespace Sales.Backend;

/// <summary>
/// Sets up Sales' data at startup: its own database, the seed data, and the
/// replication of the nightly rates to Sales' schema in the search database.
/// Every step is idempotent.
/// </summary>
class SalesSetup(
    [FromKeyedServices("sales")] NpgsqlDataSource sales,
    [FromKeyedServices("search")] NpgsqlDataSource search,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<SalesSetup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateSchema(cancellationToken);
        await GrantCompositionAccess(cancellationToken);
        await Seed(cancellationToken);

        // The publication must exist before the subscription.
        await SearchReplication.Publish(sales, "sales_search", "sales.nightly_rates", cancellationToken);
        await CreateSearchSchema(cancellationToken);
        await SearchReplication.Subscribe(search, "sales_search",
            configuration["Replication:Publisher"]!, "sales_search", cancellationToken);
        logger.LogInformation("Replicating Sales' nightly rates to the search database");
    }

    // Logical replication matches tables by schema-qualified name: the sales
    // schema here maps to the sales schema in search.
    async Task CreateSchema(CancellationToken cancellationToken)
    {
        await using var command = sales.CreateCommand("""
            CREATE SCHEMA IF NOT EXISTS sales;
            CREATE TABLE IF NOT EXISTS sales.nightly_rates (
                hotel_id uuid NOT NULL,
                night date NOT NULL,
                rate numeric(10, 2) NOT NULL,
                PRIMARY KEY (hotel_id, night)
            )
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Sales' composition handlers run in the composition gateway, and read
    // prices straight from this database: they're part of Sales, deployed
    // somewhere else. They get a role that can only read.
    async Task GrantCompositionAccess(CancellationToken cancellationToken)
    {
        await using var command = sales.CreateCommand("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sales_composition') THEN
                    CREATE ROLE sales_composition LOGIN PASSWORD 'sales_composition';
                END IF;
            END
            $$;
            GRANT USAGE ON SCHEMA sales TO sales_composition;
            GRANT SELECT ON sales.nightly_rates TO sales_composition;
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task Seed(CancellationToken cancellationToken)
    {
        await using (var count = sales.CreateCommand("SELECT count(*) FROM sales.nightly_rates"))
        {
            if ((long)(await count.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                return;
            }
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var rows = 0;

        await using var connection = await sales.OpenConnectionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY sales.nightly_rates (hotel_id, night, rate) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var rate in SalesSeed.NightlyRates(today))
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(rate.HotelId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(rate.Night, NpgsqlDbType.Date, cancellationToken);
            await writer.WriteAsync(rate.Rate, NpgsqlDbType.Numeric, cancellationToken);
            rows++;
        }

        await writer.CompleteAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} nightly rates", rows);
    }

    // Runs in the search database, as the sales role: Sales owns its schema
    // there. Same schema-qualified name as the published table. The primary
    // key covers the lookups search does: a hotel's rates over a stay.
    async Task CreateSearchSchema(CancellationToken cancellationToken)
    {
        await using var command = search.CreateCommand("""
            CREATE TABLE IF NOT EXISTS sales.nightly_rates (
                hotel_id uuid NOT NULL,
                night date NOT NULL,
                rate numeric(10, 2) NOT NULL,
                PRIMARY KEY (hotel_id, night)
            )
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
