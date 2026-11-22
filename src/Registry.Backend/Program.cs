using Hotelly.Messaging;
using Npgsql;
using Registry.Backend;
using Registry.Messages;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKeyedSingleton("registry", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("registry")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddHostedService<RegistrySetup>();

// Registered after the setup: hosted services start in order, so messages
// are processed only once the database is ready.
builder.Services.AddNServiceBusEndpoint(HotellyEndpoint.Create(RegistryEndpoint.Name, builder.Configuration));

builder.Build().Run();
