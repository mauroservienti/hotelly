using System.Reflection;

namespace Registry.Search;

/// <summary>
/// Registry's search providers, to deploy in the search host.
/// </summary>
public static class RegistrySearch
{
    public static Assembly Assembly => typeof(RegistrySearch).Assembly;
}
