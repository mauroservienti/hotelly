using Npgsql;
using Reservations.Backend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton("reservations", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("reservations")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ReservationsSetup>();

var app = builder.Build();

app.Run();
