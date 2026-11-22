using Hotelly.BackOffice;

namespace Registry.BackOffice;

/// <summary>
/// Registry's panel on the back office hotel page: the hotel's name, city,
/// stars, amenities, and description. The module is a static web asset of
/// this library, served by the website referencing it.
/// </summary>
public static class RegistryBackOffice
{
    public static BackOfficePanel Panel { get; } = new("registry", "_content/Registry.BackOffice/panel.js", 1);
}
