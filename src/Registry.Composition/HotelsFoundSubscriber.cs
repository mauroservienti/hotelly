using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Search.Composition;
using ServiceComposer.AspNetCore;

namespace Registry.Composition;

/// <summary>
/// Adds Registry's hotel details to the search results: name, city, stars,
/// and amenities. One query to Registry's database for the whole page.
/// </summary>
class HotelsFoundSubscriber([FromKeyedServices(RegistryComposition.DataSourceKey)] NpgsqlDataSource registry) : ICompositionEventsSubscriber
{
    [HttpGet("/search")]
    public void Subscribe(ICompositionEventsPublisher publisher) =>
        publisher.Subscribe<HotelsFound>(async (@event, request) =>
        {
            await using var command = registry.CreateCommand(
                "SELECT id, name, city, stars, amenities FROM registry.hotels WHERE id = ANY($1)");
            command.Parameters.AddWithValue(@event.Hotels.Keys.ToArray());

            await using var reader = await command.ExecuteReaderAsync(request.HttpContext.RequestAborted);
            while (await reader.ReadAsync(request.HttpContext.RequestAborted))
            {
                var vm = @event.Hotels[reader.GetGuid(0)];
                vm.Name = reader.GetString(1);
                vm.City = reader.GetString(2);
                vm.Stars = reader.GetInt32(3);
                vm.Amenities = reader.GetFieldValue<string[]>(4);
            }
        });
}
