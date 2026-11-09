using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceComposer.AspNetCore;

namespace Marketing.Composition;

/// <summary>
/// Registers what Marketing's composition handlers need in the gateway: a
/// connection to Marketing's database, with a role that can only read.
/// </summary>
class MarketingComposition : IViewModelCompositionOptionsCustomization
{
    public const string DataSourceKey = "marketing";

    public void Customize(ViewModelCompositionOptions options) =>
        options.Services.AddKeyedSingleton(DataSourceKey, (provider, _) =>
            NpgsqlDataSource.Create(provider.GetRequiredService<IConfiguration>().GetConnectionString("marketing")!));
}
