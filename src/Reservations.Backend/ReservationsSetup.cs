using Hotelly.Replication;
using Npgsql;
using NpgsqlTypes;

namespace Reservations.Backend;

/// <summary>
/// Sets up Reservations' data at startup: its own database, the seed data,
/// and the replication of the sold-out nights to Reservations' schema in the
/// search database. Every step is idempotent.
/// </summary>
class ReservationsSetup(
    [FromKeyedServices("reservations")] NpgsqlDataSource reservations,
    [FromKeyedServices("search")] NpgsqlDataSource search,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<ReservationsSetup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateSchema(cancellationToken);
        await Seed(cancellationToken);

        // Search only needs to know which nights are sold out. The row filter
        // publishes only those: when a booking sells out a night, the row is
        // replicated as an insert; when a cancellation frees a room, as a
        // delete.
        // The publication must exist before the subscription.
        await SearchReplication.Publish(reservations, "reservations_search",
            "reservations.availability WHERE (booked >= rooms)", cancellationToken);
        await CreateSearchSchema(cancellationToken);
        await SearchReplication.Subscribe(search, "reservations_search",
            configuration["Replication:Publisher"]!, "reservations_search", cancellationToken);
        logger.LogInformation("Replicating Reservations' sold-out nights to the search database");
    }

    // Logical replication matches tables by schema-qualified name: the
    // reservations schema here maps to the reservations schema in search.
    // A row filter on a table that publishes updates can only use columns of
    // the replica identity. The filter uses rooms and booked, so the replica
    // identity is the full row, and the whole row is published: a column
    // list must include all the replica identity columns.
    async Task CreateSchema(CancellationToken cancellationToken)
    {
        await using var command = reservations.CreateCommand("""
            CREATE SCHEMA IF NOT EXISTS reservations;
            CREATE TABLE IF NOT EXISTS reservations.availability (
                hotel_id uuid NOT NULL,
                night date NOT NULL,
                rooms integer NOT NULL,
                booked integer NOT NULL,
                PRIMARY KEY (hotel_id, night),
                CHECK (booked BETWEEN 0 AND rooms)
            );
            ALTER TABLE reservations.availability REPLICA IDENTITY FULL
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task Seed(CancellationToken cancellationToken)
    {
        await using (var count = reservations.CreateCommand("SELECT count(*) FROM reservations.availability"))
        {
            if ((long)(await count.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                return;
            }
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var rows = 0;

        await using var connection = await reservations.OpenConnectionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY reservations.availability (hotel_id, night, rooms, booked) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var night in ReservationsSeed.Availability(today))
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(night.HotelId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(night.Night, NpgsqlDbType.Date, cancellationToken);
            await writer.WriteAsync(night.Rooms, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(night.Booked, NpgsqlDbType.Integer, cancellationToken);
            rows++;
        }

        await writer.CompleteAsync(cancellationToken);
        logger.LogInformation("Seeded availability for {Count} nights", rows);
    }

    // Runs in the search database, as the reservations role: Reservations
    // owns its schema there. Same schema-qualified name and columns as the
    // published table, but only sold-out nights end up here. The primary key
    // covers the lookups search does, and lets the subscription find the rows
    // to update or delete.
    async Task CreateSearchSchema(CancellationToken cancellationToken)
    {
        await using var command = search.CreateCommand("""
            CREATE TABLE IF NOT EXISTS reservations.availability (
                hotel_id uuid NOT NULL,
                night date NOT NULL,
                rooms integer NOT NULL,
                booked integer NOT NULL,
                PRIMARY KEY (hotel_id, night)
            )
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
