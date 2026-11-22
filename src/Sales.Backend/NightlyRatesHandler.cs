using Npgsql;
using Sales.Messages.Commands;

namespace Sales.Backend;

/// <summary>
/// Sets nightly rates, as the back office asks through the composition
/// gateway. Idempotent: setting the same rates twice changes nothing. Logical
/// replication takes the changes to the search database.
/// </summary>
class NightlyRatesHandler([FromKeyedServices("sales")] NpgsqlDataSource sales) : IHandleMessages<SetNightlyRates>
{
    public async Task Handle(SetNightlyRates message, IMessageHandlerContext context)
    {
        await using var command = sales.CreateCommand("""
            INSERT INTO sales.nightly_rates (hotel_id, night, rate)
            SELECT $1, night::date, $4
            FROM generate_series($2::date, $3::date - 1, interval '1 day') AS night
            ON CONFLICT (hotel_id, night) DO UPDATE SET rate = EXCLUDED.rate
            """);
        command.Parameters.AddWithValue(message.HotelId);
        command.Parameters.AddWithValue(message.From);
        command.Parameters.AddWithValue(message.Until);
        command.Parameters.AddWithValue(message.Rate);
        await command.ExecuteNonQueryAsync(context.CancellationToken);
    }
}
