namespace Reservations.Messages.Commands;

/// <summary>
/// Sets how many rooms the hotel has for every night from
/// <paramref name="From"/> to <paramref name="Until"/>, excluded.
/// </summary>
public record SetRooms(Guid HotelId, DateOnly From, DateOnly Until, int Rooms);
