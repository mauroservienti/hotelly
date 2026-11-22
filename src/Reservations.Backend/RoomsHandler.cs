using Npgsql;
using Reservations.Messages.Commands;

namespace Reservations.Backend;

/// <summary>
/// Sets the rooms a hotel has, as the back office asks through the
/// composition gateway. Idempotent: setting the same rooms twice changes
/// nothing. A night can't have fewer rooms than bookings: it keeps one room
/// per booking, and is sold out. Logical replication takes the nights that
/// become sold out, or stop being sold out, to the search database.
/// </summary>
class RoomsHandler([FromKeyedServices("reservations")] NpgsqlDataSource reservations) : IHandleMessages<SetRooms>
{
    public async Task Handle(SetRooms message, IMessageHandlerContext context)
    {
        await using var command = reservations.CreateCommand("""
            INSERT INTO reservations.availability (hotel_id, night, rooms, booked)
            SELECT $1, night::date, $4, 0
            FROM generate_series($2::date, $3::date - 1, interval '1 day') AS night
            ON CONFLICT (hotel_id, night) DO UPDATE SET rooms = GREATEST(EXCLUDED.rooms, availability.booked)
            """);
        command.Parameters.AddWithValue(message.HotelId);
        command.Parameters.AddWithValue(message.From);
        command.Parameters.AddWithValue(message.Until);
        command.Parameters.AddWithValue(message.Rooms);
        await command.ExecuteNonQueryAsync(context.CancellationToken);
    }
}
