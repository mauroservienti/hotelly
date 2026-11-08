using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Search;

public static class SearchServiceCollectionExtensions
{
    static readonly Type[] ProviderTypes =
    [
        typeof(ISearchModelContributor),
        typeof(IHotelsSource),
        typeof(IFilterProvider),
        typeof(IFacetProvider),
        typeof(ISortProvider),
    ];

    /// <summary>
    /// Registers the search process, and discovers the providers services
    /// deploy in the given assemblies, the same way ServiceComposer discovers
    /// composition handlers.
    /// </summary>
    public static IServiceCollection AddHotelSearch(this IServiceCollection services, string connectionString, params Assembly[] providerAssemblies)
    {
        services.AddDbContext<SearchDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddScoped<HotelSearch>();

        var types = providerAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in types)
        {
            foreach (var providerType in ProviderTypes.Where(p => p.IsAssignableFrom(type)))
            {
                services.AddSingleton(providerType, type);
            }
        }

        return services;
    }
}
