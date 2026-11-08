using System.Reflection;

namespace Sales.Search;

/// <summary>
/// Sales' search providers, to deploy in the search host.
/// </summary>
public static class SalesSearch
{
    public static Assembly Assembly => typeof(SalesSearch).Assembly;
}
