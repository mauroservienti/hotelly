using Marketing.Search;
using Registry.Search;
using Reservations.Search;
using Sales.Search;
using Search;
using ServiceComposer.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Services deploy their search providers here, the same way they deploy
// composition handlers.
builder.Services.AddHotelSearch(
    builder.Configuration.GetConnectionString("search")!,
    RegistrySearch.Assembly,
    SalesSearch.Assembly,
    ReservationsSearch.Assembly,
    MarketingSearch.Assembly);

// Services' composition handlers call their APIs by name, like
// http://registry-api: Aspire tells the gateway where they are.
builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

// Composition handlers are found by scanning the assemblies deployed with the
// gateway.
builder.Services.AddViewModelComposition();

var app = builder.Build();

app.MapCompositionHandlers();

app.Run();
