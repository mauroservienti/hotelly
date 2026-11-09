using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceComposer.AspNetCore;

namespace Registry.Composition;

/// <summary>
/// Registers what Registry's composition handlers need in the gateway: a
/// connection to Registry's database, with a role that can only read.
/// </summary>
class RegistryComposition : IViewModelCompositionOptionsCustomization
{
    public const string DataSourceKey = "registry";

    public void Customize(ViewModelCompositionOptions options) =>
        options.Services.AddKeyedSingleton(DataSourceKey, (provider, _) =>
            NpgsqlDataSource.Create(provider.GetRequiredService<IConfiguration>().GetConnectionString("registry")!));
}
