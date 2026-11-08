using System.Reflection;

namespace Reservations.Search;

/// <summary>
/// Reservations' search providers, to deploy in the search host.
/// </summary>
public static class ReservationsSearch
{
    public static Assembly Assembly => typeof(ReservationsSearch).Assembly;
}
