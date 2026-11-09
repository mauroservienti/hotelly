using System.Dynamic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ServiceComposer.AspNetCore;

namespace Search.Composition;

/// <summary>
/// Runs the search, and raises <see cref="HotelsFound"/> so that services can
/// compose the results page. The search only knows the hotel IDs: everything
/// else on the page comes from the services owning it.
/// </summary>
class SearchHandler(HotelSearch search) : ICompositionRequestsHandler
{
    [HttpGet("/search")]
    public async Task Handle(HttpRequest request)
    {
        var searchRequest = SearchRequestParser.Parse(request.Query);
        var result = await search.Search(searchRequest, request.HttpContext.RequestAborted);

        // In search order: the event hands them out by ID.
        var hotels = result.HotelIds.Select(id =>
        {
            dynamic hotel = new ExpandoObject();
            hotel.Id = id;
            return hotel;
        }).ToList();

        // Nothing to compose for an empty page.
        if (hotels.Count > 0)
        {
            await request.GetCompositionContext().RaiseEvent(new HotelsFound(
                searchRequest,
                hotels.ToDictionary(h => (Guid)h.Id, h => h)));
        }

        var vm = request.GetComposedResponseModel();
        vm.Hotels = hotels;
        vm.TotalCount = result.TotalCount;
        vm.Facets = result.Facets;
    }
}
