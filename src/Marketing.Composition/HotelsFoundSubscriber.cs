using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Search.Composition;
using ServiceComposer.AspNetCore;

namespace Marketing.Composition;

/// <summary>
/// Adds Marketing's ratings to the search results: the average rating, the
/// same one the rating sort uses, and the number of reviews. One query to
/// Marketing's database for the whole page.
/// </summary>
class HotelsFoundSubscriber([FromKeyedServices(MarketingComposition.DataSourceKey)] NpgsqlDataSource marketing) : ICompositionEventsSubscriber
{
    [HttpGet("/search")]
    public void Subscribe(ICompositionEventsPublisher publisher) =>
        publisher.Subscribe<HotelsFound>(async (@event, request) =>
        {
            // Hotels without reviews have no rating, rather than a 0 rating.
            foreach (var vm in @event.Hotels.Values)
            {
                vm.Rating = null;
                vm.ReviewCount = 0;
            }

            await using var command = marketing.CreateCommand("""
                SELECT hotel_id, round(avg(rating), 1), count(*) FROM marketing.reviews
                WHERE hotel_id = ANY($1)
                GROUP BY hotel_id
                """);
            command.Parameters.AddWithValue(@event.Hotels.Keys.ToArray());

            await using var reader = await command.ExecuteReaderAsync(request.HttpContext.RequestAborted);
            while (await reader.ReadAsync(request.HttpContext.RequestAborted))
            {
                var vm = @event.Hotels[reader.GetGuid(0)];
                vm.Rating = reader.GetDecimal(1);
                vm.ReviewCount = reader.GetInt64(2);
            }
        });
}
