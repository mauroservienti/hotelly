using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceComposer.AspNetCore;

namespace Marketing.Composition;

/// <summary>
/// Shows the hotel's rating and latest reviews on the back office hotel page.
/// Guests write reviews, not hotel managers: Marketing's panel is read-only,
/// and Marketing takes no part in creating a hotel.
/// </summary>
class BackOfficeHotelHandler([FromKeyedServices(MarketingComposition.DataSourceKey)] NpgsqlDataSource marketing) : ICompositionRequestsHandler
{
    const int LatestReviews = 3;

    [HttpGet("/back-office/hotels/{id:guid}")]
    public async Task Handle(HttpRequest request)
    {
        var id = Guid.Parse((string)request.RouteValues["id"]!);
        var cancellationToken = request.HttpContext.RequestAborted;
        await using var batch = marketing.CreateBatch();
        batch.BatchCommands.Add(new NpgsqlBatchCommand(
            "SELECT round(avg(rating), 1), count(*) FROM marketing.reviews WHERE hotel_id = $1") { Parameters = { new() { Value = id } } });
        batch.BatchCommands.Add(new NpgsqlBatchCommand($"""
            SELECT rating, comment, reviewed_on FROM marketing.reviews
            WHERE hotel_id = $1 ORDER BY reviewed_on DESC LIMIT {LatestReviews}
            """) { Parameters = { new() { Value = id } } });

        await using var reader = await batch.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var rating = reader.IsDBNull(0) ? (decimal?)null : reader.GetDecimal(0);
        var reviewCount = reader.GetInt64(1);

        await reader.NextResultAsync(cancellationToken);
        var reviews = new List<object>();
        while (await reader.ReadAsync(cancellationToken))
        {
            reviews.Add(new { Rating = reader.GetInt16(0), Comment = reader.GetString(1), ReviewedOn = reader.GetFieldValue<DateOnly>(2) });
        }

        var vm = request.GetComposedResponseModel();
        vm.Rating = rating;
        vm.ReviewCount = reviewCount;
        vm.LatestReviews = reviews;
    }
}
