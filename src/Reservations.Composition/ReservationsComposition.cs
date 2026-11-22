using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceComposer.AspNetCore;

namespace Reservations.Composition;

/// <summary>
/// Registers what Reservations' composition handlers need in the gateway: a
/// connection to Reservations' database, with a role that can only read.
/// </summary>
class ReservationsComposition : IViewModelCompositionOptionsCustomization
{
    public const string DataSourceKey = "reservations";

    public void Customize(ViewModelCompositionOptions options) =>
        options.Services.AddKeyedSingleton(DataSourceKey, (provider, _) =>
            NpgsqlDataSource.Create(provider.GetRequiredService<IConfiguration>().GetConnectionString("reservations")!));
}
