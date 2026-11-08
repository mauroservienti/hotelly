using Npgsql;
using Sales.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton("sales", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("sales")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<SalesSetup>();

var app = builder.Build();

app.Run();
