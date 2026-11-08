using Marketing.Api;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton("marketing", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("marketing")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<MarketingSetup>();

var app = builder.Build();

app.Run();
