using System.Security.Cryptography;
using System.Text;
using Hotelly.Seed;

namespace Reservations.Backend;

record SeedNight(Guid HotelId, DateOnly Night, int Rooms, int Booked);

static class ReservationsSeed
{
    public const int Days = 365;

    /// <summary>
    /// A year of room availability for every hotel, from the first day of the
    /// current month. Busier in high season and on weekends, with some nights
    /// sold out. A night's availability is the same at every run, whatever the
    /// day the demo runs.
    /// </summary>
    public static IEnumerable<SeedNight> Availability(DateOnly today)
    {
        var random = SeedHotels.Random(3);
        var start = new DateOnly(today.Year, today.Month, 1);

        foreach (var number in SeedHotels.Numbers)
        {
            var id = SeedHotels.Id(number);
            var rooms = random.Next(10, 121);

            for (var day = 0; day < Days; day++)
            {
                var night = start.AddDays(day);
                var occupancy = Season(night)
                    + (IsWeekend(night) ? 0.1 : 0)
                    + (Noise(number, night) - 0.5) * 0.7;
                var booked = Math.Clamp((int)Math.Round(rooms * occupancy), 0, rooms);
                yield return new SeedNight(id, night, rooms, booked);
            }
        }
    }

    // Derived from the hotel and the night, not from the position in the
    // sequence: a night is sold out or not whatever the month the demo runs.
    // Hashed, like hotel IDs: seeding Random with consecutive values gives
    // correlated first values.
    static double Noise(int number, DateOnly night)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"hotelly-night-{number}-{night.DayNumber}"));
        return BitConverter.ToUInt32(hash) / (double)uint.MaxValue;
    }

    // Reservations' own idea of the seasons: the expected occupancy.
    static double Season(DateOnly night) => night switch
    {
        { Month: 12, Day: >= 20 } or { Month: 1, Day: <= 6 } => 0.9,
        { Month: 7 or 8 } => 0.9,
        { Month: 4 or 5 or 6 or 9 } => 0.75,
        _ => 0.5,
    };

    static bool IsWeekend(DateOnly night) => night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
}
