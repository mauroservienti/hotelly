using System.Reflection;

namespace Marketing.Search;

/// <summary>
/// Marketing' search providers, to deploy in the search host.
/// </summary>
public static class MarketingSearch
{
    public static Assembly Assembly => typeof(MarketingSearch).Assembly;
}
