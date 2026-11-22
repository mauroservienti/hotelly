namespace Hotelly.BackOffice;

/// <summary>
/// A service's panel on the back office hotel page. Services ship their panels
/// as Razor class libraries: the module is a static web asset, served by the
/// website that references the library. The website hosts the panels, but
/// doesn't know what's in them.
/// </summary>
/// <param name="Service">The service owning the panel, for example <c>registry</c>.</param>
/// <param name="Module">The URL of the panel's JavaScript module.</param>
/// <param name="Order">Where the panel goes on the page.</param>
public record BackOfficePanel(string Service, string Module, int Order);
