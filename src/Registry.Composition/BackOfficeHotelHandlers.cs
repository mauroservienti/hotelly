using Heyi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NServiceBus;
using Registry.Messages;
using Registry.Messages.Commands;
using ServiceComposer.AspNetCore;

#pragma warning disable SC0001 // GetArguments is experimental.

namespace Registry.Composition;

/// <summary>
/// Shows Registry's hotel details on the back office hotel page. Registry
/// owns the hotels: if it doesn't know the hotel, the page doesn't exist.
/// </summary>
class BackOfficeHotelHandler([FromKeyedServices(RegistryComposition.DataSourceKey)] NpgsqlDataSource registry) : ICompositionRequestsHandler
{
    [HttpGet("/back-office/hotels/{id:guid}")]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        await using var command = registry.CreateCommand(
            "SELECT name, city, stars, amenities, description FROM registry.hotels WHERE id = $1");
        command.Parameters.AddWithValue(id);

        await using var reader = await command.ExecuteReaderAsync(request.HttpContext.RequestAborted);
        if (!await reader.ReadAsync(request.HttpContext.RequestAborted))
        {
            request.SetActionResult(new NotFoundResult());
            return;
        }

        var vm = request.GetComposedResponseModel();
        vm.Id = id;
        vm.Name = reader.GetString(0);
        vm.City = reader.GetString(1);
        vm.Stars = reader.GetInt32(2);
        vm.Amenities = reader.GetFieldValue<string[]>(3);
        vm.Description = reader.GetString(4);
    }
}

/// <summary>
/// Registry's part of creating a hotel. The other services' handlers for the
/// same request send their own commands: Heyi's transactional session sends
/// all of them, or none. Rejecting the request rolls back the others' too.
/// </summary>
class BackOfficeNewHotelHandler(IOutgoingMessages messages) : ICompositionRequestsHandler
{
    [HttpPost("/back-office/hotels/{id:guid}")]
    [BindFromBody<NewHotelRequest>]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        var details = request.GetCompositionContext().GetArguments(this).Argument<NewHotelRequest>()?.Registry ?? new HotelDetails();
        if (details.Validate() is { } rejection)
        {
            request.SetActionResult(rejection);
            return;
        }

        var options = new SendOptions();
        options.SetDestination(RegistryEndpoint.Name);
        await messages.Send(
            new AddHotel(id, details.Name!.Trim(), details.City!.Trim(), details.Stars, details.NormalizedAmenities(), details.Description?.Trim() ?? ""),
            options,
            request.HttpContext.RequestAborted);
    }
}

/// <summary>
/// Saves Registry's panel on the back office hotel page.
/// </summary>
class BackOfficeHotelDetailsHandler(IOutgoingMessages messages) : ICompositionRequestsHandler
{
    [HttpPost("/back-office/hotels/{id:guid}/registry")]
    [BindFromBody<HotelDetails>]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        var details = request.GetCompositionContext().GetArguments(this).Argument<HotelDetails>() ?? new HotelDetails();
        if (details.Validate() is { } rejection)
        {
            request.SetActionResult(rejection);
            return;
        }

        var options = new SendOptions();
        options.SetDestination(RegistryEndpoint.Name);
        await messages.Send(
            new UpdateHotelDetails(id, details.Name!.Trim(), details.City!.Trim(), details.Stars, details.NormalizedAmenities(), details.Description?.Trim() ?? ""),
            options,
            request.HttpContext.RequestAborted);
    }
}
