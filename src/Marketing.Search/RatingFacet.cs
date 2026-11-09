using Microsoft.EntityFrameworkCore;
using Search;

namespace Marketing.Search;

/// <summary>
/// How many of the filtered hotels are rated 4 and up, 3 and up, and 2 and
/// up, on average. The bands overlap: a hotel rated 4.5 counts in all three.
/// Every reviewed hotel is rated 1 and up, so there's no band for it. Each
/// value is what the rating filter expects.
/// </summary>
class RatingFacet : IFacetProvider
{
    static readonly int[] Bands = [4, 3, 2];

    public async Task<Facet> Build(SearchDbContext db, IQueryable<Guid> filtered, SearchRequest request, CancellationToken cancellationToken)
    {
        // One query: how many hotels have an average rating in each whole
        // number, then the bands add them up.
        var counts = await db.Set<Review>()
            .Where(r => filtered.Contains(r.HotelId))
            .GroupBy(r => r.HotelId)
            .Select(g => Math.Floor(g.Average(r => (double)r.Rating)))
            .GroupBy(floor => floor)
            .Select(g => new { Floor = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new Facet("rating", Bands
            .Select(band => new FacetValue(band.ToString(), counts.Where(c => c.Floor >= band).Sum(c => c.Count)))
            .ToList());
    }
}
