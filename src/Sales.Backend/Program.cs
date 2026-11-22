using Hotelly.Messaging;
using Npgsql;
using Sales.Backend;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKeyedSingleton("sales", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("sales")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<SalesSetup>();

// Registered after the setup: hosted services start in order, so messages
// are processed only once the database is ready.
builder.Services.AddNServiceBusEndpoint(HotellyEndpoint.Create("Hotelly.Sales", builder.Configuration));

builder.Build().Run();
