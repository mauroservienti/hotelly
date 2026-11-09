using Npgsql;
using Registry.Backend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton("registry", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("registry")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddHostedService<RegistrySetup>();

var app = builder.Build();

app.Run();
