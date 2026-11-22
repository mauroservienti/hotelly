using Hotelly.Messaging;
using Npgsql;
using Reservations.Backend;
using Reservations.Messages;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKeyedSingleton("reservations", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("reservations")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ReservationsSetup>();

// Registered after the setup: hosted services start in order, so messages
// are processed only once the database is ready.
builder.Services.AddNServiceBusEndpoint(HotellyEndpoint.Create(ReservationsEndpoint.Name, builder.Configuration));

builder.Build().Run();
