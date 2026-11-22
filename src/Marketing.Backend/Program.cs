using Hotelly.Messaging;
using Marketing.Backend;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKeyedSingleton("marketing", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("marketing")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<MarketingSetup>();

// Registered after the setup: hosted services start in order, so messages
// are processed only once the database is ready.
builder.Services.AddNServiceBusEndpoint(HotellyEndpoint.Create("Hotelly.Marketing", builder.Configuration));

builder.Build().Run();
