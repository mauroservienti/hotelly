using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Search.Composition;
using ServiceComposer.AspNetCore;

namespace Sales.Composition;

/// <summary>
/// Adds Sales' prices to the search results, the same ones the price sort
/// uses. With stay dates, the price of the stay: the sum of the nightly rates.
/// Without stay dates, the "from" price: the lowest nightly rate in the next
/// 30 days. One query to Sales' database for the whole page.
/// </summary>
class HotelsFoundSubscriber(
    [FromKeyedServices(SalesComposition.DataSourceKey)] NpgsqlDataSource sales,
    TimeProvider time) : ICompositionEventsSubscriber
{
    // Must match the price sort's window, in Sales.Search.
    const int FromPriceDays = 30;

    [HttpGet("/search")]
    public void Subscribe(ICompositionEventsPublisher publisher) =>
        publisher.Subscribe<HotelsFound>(async (@event, request) =>
        {
            var stay = @event.Request.Stay;
            var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
            var (aggregate, from, until) = stay is null
                ? ("min", today, today.AddDays(FromPriceDays))
                : ("sum", stay.CheckIn, stay.CheckOut);

            await using var command = sales.CreateCommand($"""
                SELECT hotel_id, {aggregate}(rate) FROM sales.nightly_rates
                WHERE hotel_id = ANY($1) AND night >= $2 AND night < $3
                GROUP BY hotel_id
                """);
            command.Parameters.AddWithValue(@event.Hotels.Keys.ToArray());
            command.Parameters.AddWithValue(from);
            command.Parameters.AddWithValue(until);

            var prices = new Dictionary<Guid, decimal>();
            await using (var reader = await command.ExecuteReaderAsync(request.HttpContext.RequestAborted))
            {
                while (await reader.ReadAsync(request.HttpContext.RequestAborted))
                {
                    prices[reader.GetGuid(0)] = reader.GetDecimal(1);
                }
            }

            // With stay dates, the search only returns hotels with a rate for
            // every night, so they all have a price. Without, a hotel with no
            // rate in the next 30 days has none: null, not a missing property.
            foreach (var (id, vm) in @event.Hotels)
            {
                decimal? price = prices.TryGetValue(id, out var p) ? p : null;
                if (stay is null)
                {
                    vm.FromPrice = price;
                }
                else
                {
                    vm.StayPrice = price;
                }
            }
        });
}
