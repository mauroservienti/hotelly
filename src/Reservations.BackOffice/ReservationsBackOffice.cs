using Hotelly.BackOffice;

namespace Reservations.BackOffice;

/// <summary>
/// Reservations' panel on the back office hotel page: the hotel's rooms and
/// bookings. The module is a static web asset of this library, served by the
/// website referencing it.
/// </summary>
public static class ReservationsBackOffice
{
    public static BackOfficePanel Panel { get; } = new("reservations", "_content/Reservations.BackOffice/panel.js", 3);
}
