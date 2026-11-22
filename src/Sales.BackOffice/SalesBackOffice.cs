using Hotelly.BackOffice;

namespace Sales.BackOffice;

/// <summary>
/// Sales' panel on the back office hotel page: the hotel's nightly rates.
/// The module is a static web asset of this library, served by the website
/// referencing it.
/// </summary>
public static class SalesBackOffice
{
    public static BackOfficePanel Panel { get; } = new("sales", "_content/Sales.BackOffice/panel.js", 2);
}
