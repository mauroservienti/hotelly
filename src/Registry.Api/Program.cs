using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Registry.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton("registry", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("registry")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddHostedService<RegistrySetup>();

var app = builder.Build();

// Hotel details for a page of search results, in one call: the composition
// gateway asks for all the hotels on the page at once.
app.MapGet("/hotels", async (
    [FromQuery] Guid[] ids,
    [FromKeyedServices("registry")] NpgsqlDataSource registry,
    CancellationToken cancellationToken) =>
{
    await using var command = registry.CreateCommand(
        "SELECT id, name, city, stars, amenities FROM registry.hotels WHERE id = ANY($1)");
    command.Parameters.AddWithValue(ids);

    var hotels = new List<HotelDetails>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        hotels.Add(new HotelDetails(
            reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetFieldValue<string[]>(4)));
    }

    return hotels;
});

app.Run();

record HotelDetails(Guid Id, string Name, string City, int Stars, string[] Amenities);
