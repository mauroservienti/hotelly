using Npgsql;

namespace Hotelly.Replication;

/// <summary>
/// Replicates a service's search tables from its own database to its schema
/// in the search database, with PostgreSQL logical replication. Both steps
/// are idempotent.
/// </summary>
/// <remarks>
/// The publication must exist before the subscription: CREATE SUBSCRIPTION
/// asks the publisher which tables to replicate, and records them. Without the
/// publication, the subscription is created with no tables, and stays that way
/// until ALTER SUBSCRIPTION ... REFRESH PUBLICATION.
/// </remarks>
public static class SearchReplication
{
    /// <summary>
    /// Publishes the given tables, for example
    /// <c>registry.hotels (id, city, stars)</c> to publish only some columns.
    /// Runs in the service's own database.
    /// </summary>
    public static async Task Publish(NpgsqlDataSource source, string publication, string tables, CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand($"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_publication WHERE pubname = '{publication}') THEN
                    CREATE PUBLICATION {publication} FOR TABLE {tables};
                END IF;
            END
            $$
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Subscribes the search database to the publication. Runs in the search
    /// database, as the service's role: the service owns its schema there,
    /// including the subscription that fills it. Replicated tables must
    /// already exist, with the same schema-qualified names as the published
    /// ones.
    /// </summary>
    public static async Task Subscribe(NpgsqlDataSource search, string subscription, string publisherConnection, string publication, CancellationToken cancellationToken)
    {
        await using (var exists = search.CreateCommand("SELECT 1 FROM pg_subscription WHERE subname = $1"))
        {
            exists.Parameters.AddWithValue(subscription);
            if (await exists.ExecuteScalarAsync(cancellationToken) is not null)
            {
                return;
            }
        }

        // CREATE SUBSCRIPTION can't run in a transaction block, nor take parameters.
        var connection = publisherConnection.Replace("'", "''");
        await using var subscribe = search.CreateCommand(
            $"CREATE SUBSCRIPTION {subscription} CONNECTION '{connection}' PUBLICATION {publication}");
        await subscribe.ExecuteNonQueryAsync(cancellationToken);
    }
}
