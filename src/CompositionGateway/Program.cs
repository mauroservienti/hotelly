using CompositionGateway;
using Marketing.Search;
using Registry.Search;
using Reservations.Search;
using Sales.Search;
using Search;

var builder = WebApplication.CreateBuilder(args);

// Services deploy their search providers here, the same way they deploy
// composition handlers.
builder.Services.AddHotelSearch(
    builder.Configuration.GetConnectionString("search")!,
    RegistrySearch.Assembly,
    SalesSearch.Assembly,
    ReservationsSearch.Assembly,
    MarketingSearch.Assembly);

var app = builder.Build();

app.MapGet("/search", (HttpRequest request, HotelSearch search, CancellationToken cancellationToken) =>
    search.Search(SearchRequestParser.Parse(request.Query), cancellationToken));

app.Run();
