using Hotelly.BackOffice;

namespace Marketing.BackOffice;

/// <summary>
/// Marketing's panel on the back office hotel page: the hotel's rating and
/// latest reviews, read-only. The module is a static web asset of this
/// library, served by the website referencing it.
/// </summary>
public static class MarketingBackOffice
{
    public static BackOfficePanel Panel { get; } = new("marketing", "_content/Marketing.BackOffice/panel.js", 4);
}
