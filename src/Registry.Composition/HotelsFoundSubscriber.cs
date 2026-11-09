using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Search.Composition;
using ServiceComposer.AspNetCore;

namespace Registry.Composition;

/// <summary>
/// Adds Registry's hotel details to the search results: name, city, stars,
/// and amenities. One call to Registry for the whole page.
/// </summary>
class HotelsFoundSubscriber(IHttpClientFactory httpClientFactory) : ICompositionEventsSubscriber
{
    [HttpGet("/search")]
    public void Subscribe(ICompositionEventsPublisher publisher) =>
        publisher.Subscribe<HotelsFound>(async (@event, request) =>
        {
            var ids = string.Join("&", @event.Hotels.Keys.Select(id => $"ids={id}"));
            var details = await httpClientFactory.CreateClient(RegistryComposition.HttpClientName)
                .GetFromJsonAsync<HotelDetails[]>($"/hotels?{ids}", request.HttpContext.RequestAborted);

            foreach (var hotel in details!)
            {
                var vm = @event.Hotels[hotel.Id];
                vm.Name = hotel.Name;
                vm.City = hotel.City;
                vm.Stars = hotel.Stars;
                vm.Amenities = hotel.Amenities;
            }
        });

    record HotelDetails(Guid Id, string Name, string City, int Stars, string[] Amenities);
}
