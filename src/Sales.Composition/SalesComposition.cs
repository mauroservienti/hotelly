using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceComposer.AspNetCore;

namespace Sales.Composition;

/// <summary>
/// Registers what Sales' composition handlers need in the gateway: a
/// connection to Sales' database, with a role that can only read.
/// </summary>
class SalesComposition : IViewModelCompositionOptionsCustomization
{
    public const string DataSourceKey = "sales";

    public void Customize(ViewModelCompositionOptions options) =>
        options.Services.AddKeyedSingleton(DataSourceKey, (provider, _) =>
            NpgsqlDataSource.Create(provider.GetRequiredService<IConfiguration>().GetConnectionString("sales")!));
}
