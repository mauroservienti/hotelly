using Npgsql;
using Registry.Messages.Commands;

namespace Registry.Backend;

/// <summary>
/// Handles the commands the back office sends through the composition
/// gateway. Both are idempotent: a duplicate AddHotel finds the hotel already
/// there, and a duplicate UpdateHotelDetails writes the same values again.
/// Logical replication takes the changes to the search database.
/// </summary>
class HotelCommandsHandler([FromKeyedServices("registry")] NpgsqlDataSource registry)
    : IHandleMessages<AddHotel>, IHandleMessages<UpdateHotelDetails>
{
    public async Task Handle(AddHotel message, IMessageHandlerContext context)
    {
        await using var command = registry.CreateCommand("""
            INSERT INTO registry.hotels (id, name, city, stars, amenities, description)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (id) DO NOTHING
            """);
        AddParameters(command, message.HotelId, message.Name, message.City, message.Stars, message.Amenities, message.Description);
        await command.ExecuteNonQueryAsync(context.CancellationToken);
    }

    public async Task Handle(UpdateHotelDetails message, IMessageHandlerContext context)
    {
        await using var command = registry.CreateCommand("""
            UPDATE registry.hotels
            SET name = $2, city = $3, stars = $4, amenities = $5, description = $6
            WHERE id = $1
            """);
        AddParameters(command, message.HotelId, message.Name, message.City, message.Stars, message.Amenities, message.Description);
        await command.ExecuteNonQueryAsync(context.CancellationToken);
    }

    static void AddParameters(NpgsqlCommand command, Guid id, string name, string city, int stars, string[] amenities, string description)
    {
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(city);
        command.Parameters.AddWithValue(stars);
        command.Parameters.AddWithValue(amenities);
        command.Parameters.AddWithValue(description);
    }
}
