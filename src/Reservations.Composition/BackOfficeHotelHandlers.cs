using Heyi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NServiceBus;
using Reservations.Messages;
using Reservations.Messages.Commands;
using ServiceComposer.AspNetCore;

#pragma warning disable SC0001 // GetArguments is experimental.

namespace Reservations.Composition;

/// <summary>
/// Shows the hotel's rooms and bookings for the next two weeks on the back
/// office hotel page.
/// </summary>
class BackOfficeHotelHandler(
    [FromKeyedServices(ReservationsComposition.DataSourceKey)] NpgsqlDataSource reservations,
    TimeProvider time) : ICompositionRequestsHandler
{
    const int Nights = 14;

    [HttpGet("/back-office/hotels/{id:guid}")]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        await using var command = reservations.CreateCommand("""
            SELECT night, rooms, booked FROM reservations.availability
            WHERE hotel_id = $1 AND night >= $2 AND night < $3
            ORDER BY night
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(today);
        command.Parameters.AddWithValue(today.AddDays(Nights));

        var availability = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(request.HttpContext.RequestAborted);
        while (await reader.ReadAsync(request.HttpContext.RequestAborted))
        {
            availability.Add(new { Night = reader.GetFieldValue<DateOnly>(0), Rooms = reader.GetInt32(1), Booked = reader.GetInt32(2) });
        }

        request.GetComposedResponseModel().Availability = availability;
    }
}

/// <summary>
/// Reservations' part of creating a hotel: its rooms for the next year. Sent
/// in the same transactional session as the other services' commands.
/// </summary>
class BackOfficeNewHotelHandler(IOutgoingMessages messages, TimeProvider time) : ICompositionRequestsHandler
{
    [HttpPost("/back-office/hotels/{id:guid}")]
    [BindFromBody<NewHotelRequest>]
    public async Task Handle(HttpRequest request)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var rooms = new Rooms
        {
            From = today,
            Until = today.AddDays(365),
            Count = request.GetCompositionContext().GetArguments(this).Argument<NewHotelRequest>()?.Reservations?.Rooms ?? 0
        };
        await Send(messages, request, Guid.Parse((string)request.RouteValues["id"]!), rooms, today);
    }

    internal static async Task Send(IOutgoingMessages messages, HttpRequest request, Guid id, Rooms rooms, DateOnly today)
    {
        if (rooms.Validate(today) is { } rejection)
        {
            request.SetActionResult(rejection);
            return;
        }

        var options = new SendOptions();
        options.SetDestination(ReservationsEndpoint.Name);
        await messages.Send(new SetRooms(id, rooms.From!.Value, rooms.Until!.Value, rooms.Count), options, request.HttpContext.RequestAborted);
    }
}

/// <summary>
/// Saves Reservations' panel on the back office hotel page.
/// </summary>
class BackOfficeRoomsHandler(IOutgoingMessages messages, TimeProvider time) : ICompositionRequestsHandler
{
    [HttpPost("/back-office/hotels/{id:guid}/reservations")]
    [BindFromBody<Rooms>]
    public Task Handle(HttpRequest request) =>
        BackOfficeNewHotelHandler.Send(
            messages,
            request,
            Guid.Parse((string)request.RouteValues["id"]!),
            request.GetCompositionContext().GetArguments(this).Argument<Rooms>() ?? new Rooms(),
            DateOnly.FromDateTime(time.GetLocalNow().Date));
}
