using Heyi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NServiceBus;
using Sales.Messages;
using Sales.Messages.Commands;
using ServiceComposer.AspNetCore;

#pragma warning disable SC0001 // GetArguments is experimental.

namespace Sales.Composition;

/// <summary>
/// Shows the hotel's nightly rates for the next two weeks on the back office
/// hotel page.
/// </summary>
class BackOfficeHotelHandler(
    [FromKeyedServices(SalesComposition.DataSourceKey)] NpgsqlDataSource sales,
    TimeProvider time) : ICompositionRequestsHandler
{
    const int Nights = 14;

    [HttpGet("/back-office/hotels/{id:guid}")]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        await using var command = sales.CreateCommand("""
            SELECT night, rate FROM sales.nightly_rates
            WHERE hotel_id = $1 AND night >= $2 AND night < $3
            ORDER BY night
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(today);
        command.Parameters.AddWithValue(today.AddDays(Nights));

        var rates = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(request.HttpContext.RequestAborted);
        while (await reader.ReadAsync(request.HttpContext.RequestAborted))
        {
            rates.Add(new { Night = reader.GetFieldValue<DateOnly>(0), Rate = reader.GetDecimal(1) });
        }

        request.GetComposedResponseModel().NightlyRates = rates;
    }
}

/// <summary>
/// Sales' part of creating a hotel: one nightly rate for the next year. Sent
/// in the same transactional session as the other services' commands.
/// </summary>
class BackOfficeNewHotelHandler(IOutgoingMessages messages, TimeProvider time) : ICompositionRequestsHandler
{
    [HttpPost("/back-office/hotels/{id:guid}")]
    [BindFromBody<NewHotelRequest>]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var rates = new NightlyRates
        {
            From = today,
            Until = today.AddDays(365),
            Rate = request.GetCompositionContext().GetArguments(this).Argument<NewHotelRequest>()?.Sales?.NightlyRate ?? 0
        };
        await Send(messages, request, id, rates, today);
    }

    internal static async Task Send(IOutgoingMessages messages, HttpRequest request, Guid id, NightlyRates rates, DateOnly today)
    {
        if (rates.Validate(today) is { } rejection)
        {
            request.SetActionResult(rejection);
            return;
        }

        var options = new SendOptions();
        options.SetDestination(SalesEndpoint.Name);
        await messages.Send(new SetNightlyRates(id, rates.From!.Value, rates.Until!.Value, rates.Rate), options, request.HttpContext.RequestAborted);
    }
}

/// <summary>
/// Saves Sales' panel on the back office hotel page.
/// </summary>
class BackOfficeNightlyRatesHandler(IOutgoingMessages messages, TimeProvider time) : ICompositionRequestsHandler
{
    [HttpPost("/back-office/hotels/{id:guid}/sales")]
    [BindFromBody<NightlyRates>]
    public Task Handle(HttpRequest request) =>
        BackOfficeNewHotelHandler.Send(
            messages,
            request,
            Guid.Parse((string)request.RouteValues["id"]!),
            request.GetCompositionContext().GetArguments(this).Argument<NightlyRates>() ?? new NightlyRates(),
            DateOnly.FromDateTime(time.GetLocalNow().Date));
}
