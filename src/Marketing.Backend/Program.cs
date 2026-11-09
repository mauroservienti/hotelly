using Marketing.Backend;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKeyedSingleton("marketing", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("marketing")!));
builder.Services.AddKeyedSingleton("search", (_, _) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("search")!));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<MarketingSetup>();

builder.Build().Run();
