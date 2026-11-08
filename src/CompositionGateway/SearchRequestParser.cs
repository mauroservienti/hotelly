using Microsoft.Extensions.Primitives;
using Search;

namespace CompositionGateway;

/// <summary>
/// Turns a query string into a search request:
/// <c>/search?city=Rome&amp;stars=4,5&amp;checkIn=2027-02-10&amp;checkOut=2027-02-13&amp;orderBy=price,stars:desc&amp;page=2</c>.
/// Every parameter that isn't reserved is a filter, so services can add
/// filters without the gateway knowing about them.
/// </summary>
static class SearchRequestParser
{
    static readonly HashSet<string> Reserved = new(["checkIn", "checkOut", "orderBy", "page", "pageSize"], StringComparer.OrdinalIgnoreCase);

    public static SearchRequest Parse(IQueryCollection query)
    {
        var request = new SearchRequest
        {
            Filters = query
                .Where(p => !Reserved.Contains(p.Key))
                .ToDictionary(p => p.Key, p => Values(p.Value), StringComparer.OrdinalIgnoreCase),
            OrderBy = Values(query["orderBy"])
                .Select(s => s.Split(':'))
                .Select(s => new SortBy(s[0], s.Length > 1 && s[1].Equals("desc", StringComparison.OrdinalIgnoreCase)))
                .ToList(),
        };

        if (DateOnly.TryParse(query["checkIn"], out var checkIn)
            && DateOnly.TryParse(query["checkOut"], out var checkOut)
            && checkOut > checkIn)
        {
            request = request with { Stay = new StayDates(checkIn, checkOut) };
        }

        if (int.TryParse(query["page"], out var page))
        {
            request = request with { Page = page };
        }

        if (int.TryParse(query["pageSize"], out var pageSize))
        {
            request = request with { PageSize = pageSize };
        }

        return request;
    }

    // Both ?stars=4&stars=5 and ?stars=4,5 work.
    static string[] Values(StringValues values) =>
        values
            .SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
}
