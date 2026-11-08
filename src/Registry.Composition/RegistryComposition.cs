using Microsoft.Extensions.DependencyInjection;
using ServiceComposer.AspNetCore;

namespace Registry.Composition;

/// <summary>
/// Registers what Registry's composition handlers need in the gateway: an
/// HTTP client for Registry's API, found through Aspire service discovery.
/// </summary>
class RegistryComposition : IViewModelCompositionOptionsCustomization
{
    public const string HttpClientName = "registry";

    public void Customize(ViewModelCompositionOptions options) =>
        options.Services.AddHttpClient(HttpClientName, client => client.BaseAddress = new Uri("http://registry-api"));
}
