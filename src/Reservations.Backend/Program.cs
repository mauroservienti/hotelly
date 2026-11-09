using Npgsql;
using Reservations.Backend;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKeyedSingleton("reservations", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("reservations")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ReservationsSetup>();

builder.Build().Run();
