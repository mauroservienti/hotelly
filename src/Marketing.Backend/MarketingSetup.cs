using Hotelly.Replication;
using Npgsql;
using NpgsqlTypes;

namespace Marketing.Backend;

/// <summary>
/// Sets up Marketing's data at startup: its own database, the seed data, and
/// the replication of the ratings to Marketing's schema in the search
/// database. Every step is idempotent.
/// </summary>
class MarketingSetup(
    [FromKeyedServices("marketing")] NpgsqlDataSource marketing,
    [FromKeyedServices("search")] NpgsqlDataSource search,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<MarketingSetup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateSchema(cancellationToken);
        await GrantCompositionAccess(cancellationToken);
        await Seed(cancellationToken);

        // Search needs ratings, not what reviewers wrote: the comments stay
        // in Marketing. Replication can't aggregate, so search gets one row
        // per review and computes the averages itself.
        // The publication must exist before the subscription.
        await SearchReplication.Publish(marketing, "marketing_search",
            "marketing.reviews (id, hotel_id, rating)", cancellationToken);
        await CreateSearchSchema(cancellationToken);
        await SearchReplication.Subscribe(search, "marketing_search",
            configuration["Replication:Publisher"]!, "marketing_search", cancellationToken);
        logger.LogInformation("Replicating Marketing's ratings to the search database");
    }

    // Logical replication matches tables by schema-qualified name: the
    // marketing schema here maps to the marketing schema in search.
    async Task CreateSchema(CancellationToken cancellationToken)
    {
        await using var command = marketing.CreateCommand("""
            CREATE SCHEMA IF NOT EXISTS marketing;
            CREATE TABLE IF NOT EXISTS marketing.reviews (
                id uuid PRIMARY KEY,
                hotel_id uuid NOT NULL,
                rating smallint NOT NULL CHECK (rating BETWEEN 1 AND 5),
                comment text NOT NULL,
                reviewed_on date NOT NULL
            );
            CREATE INDEX IF NOT EXISTS reviews_hotel_id ON marketing.reviews (hotel_id)
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Marketing's composition handlers run in the composition gateway, and
    // read ratings straight from this database: they're part of Marketing,
    // deployed somewhere else. They get a role that can only read.
    async Task GrantCompositionAccess(CancellationToken cancellationToken)
    {
        await using var command = marketing.CreateCommand("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'marketing_composition') THEN
                    CREATE ROLE marketing_composition LOGIN PASSWORD 'marketing_composition';
                END IF;
            END
            $$;
            GRANT USAGE ON SCHEMA marketing TO marketing_composition;
            GRANT SELECT ON marketing.reviews TO marketing_composition;
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task Seed(CancellationToken cancellationToken)
    {
        await using (var count = marketing.CreateCommand("SELECT count(*) FROM marketing.reviews"))
        {
            if ((long)(await count.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                return;
            }
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var rows = 0;

        await using var connection = await marketing.OpenConnectionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY marketing.reviews (id, hotel_id, rating, comment, reviewed_on) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var review in MarketingSeed.Reviews(today))
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(review.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(review.HotelId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(review.Rating, NpgsqlDbType.Smallint, cancellationToken);
            await writer.WriteAsync(review.Comment, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(review.ReviewedOn, NpgsqlDbType.Date, cancellationToken);
            rows++;
        }

        await writer.CompleteAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} reviews", rows);
    }

    // Runs in the search database, as the marketing role: Marketing owns its
    // schema there. Same schema-qualified name as the published table, with
    // only the published columns. The index covers the lookups search does:
    // a hotel's ratings.
    async Task CreateSearchSchema(CancellationToken cancellationToken)
    {
        await using var command = search.CreateCommand("""
            CREATE TABLE IF NOT EXISTS marketing.reviews (
                id uuid PRIMARY KEY,
                hotel_id uuid NOT NULL,
                rating smallint NOT NULL
            );
            CREATE INDEX IF NOT EXISTS reviews_hotel_id ON marketing.reviews (hotel_id, rating)
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
